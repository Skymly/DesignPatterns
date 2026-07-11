# Design Doc: Chain of Responsibility

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 ADR**：ADR-XXX（如有）

## 概述

责任链模式将请求沿 handler 链传递，每个 handler 决定是处理请求还是转发给下一个。本库提供 **中间件式管道 primitives**；与 MediatR 或 ASP.NET Core 管道在能力上可能重叠，但本库聚焦「源生成排序 + 编译期诊断」的探索价值（见 [AGENTS.md](../../AGENTS.md)「项目是什么」）。

## 设计目标

1. 与 ASP.NET Core 中间件一致的 **onion 调用模型**（先注册先执行 inbound）
2. 通过 **是否调用 `next`** 实现短路，不强制业务 context 实现额外接口
3. **异步一等**：`ValueTask` + `CancellationToken`
4. 不依赖 DI；handler 由调用方手动组装

## API 面

### 运行时接口

运行时类型位于 `DesignPatterns/Behavioral/`。

| 类型 | 说明 |
|------|------|
| `HandlerDelegate<TContext>` | 调用链中下一阶段的委托 |
| `IHandler<TContext>` | 单个 handler：`InvokeAsync(context, next, ct)` |
| `HandlerPipelineBuilder<TContext>` | `Use(handler)` / `Use(delegate)`，`Build()` |
| `HandlerPipeline<TContext>` | 不可变管道，`InvokeAsync(context, ct)`、`InvokeTracedAsync`（见下文） |
| `HandlerPipelineTrace` | 一次 traced 调用的逐步结果 |
| `HandlerPipelineStep` | 单步：索引、handler 显示名、状态 |
| `HandlerPipelineStepStatus` | `Completed` / `ShortCircuited` / `NotReached` |

`HandlerPipelineStepStatus` 取值含义：

| `HandlerPipelineStepStatus` | 含义 |
|----------------------------|------|
| **Completed** | Handler 已执行且调用了 `next` |
| **ShortCircuited** | Handler 已执行但**未**调用 `next`（管道在此 handler 之后不再向下） |
| **NotReached** | Handler **未执行**（因更早的 handler 短路） |

`InvokeTracedAsync` 不改变执行顺序与短路语义，仅额外返回 `HandlerPipelineTrace`；委托 handler 在 trace 中显示名为 `"<delegate>"`。

`HandlerPipelineTrace.WasShortCircuited` 为「是否存在任一步为 `ShortCircuited`」；终端 handler 不调用 `next` 时也会为 `true`，不宜单独作为「业务短路」判据。

### 特性（Attribute）

```csharp
// 泛型（推荐，C# 11+ / net8.0）
[HandlerOrder<RequestContext>(10)]
public sealed class LoggingHandler : IHandler<RequestContext> { }

// 非泛型（netstandard2.0）
[HandlerOrder(10, typeof(RequestContext))]
public sealed class LoggingHandler : IHandler<RequestContext> { }
```

数值越小越先执行（与手动 `Use` 注册顺序一致）。

同一 handler 类可标注多个 `[HandlerOrder<...>]`（`AllowMultiple = true`），分别加入不同 `TContext` 的生成管道，例如共享日志 handler 同时实现 `IHandler<RequestContext>` 与 `IHandler<AuditContext>`。同一 context 下重复的 Order 仍报 **DP005**。

### 生成器产出

对每种 `TContext` 生成 `{Context}HandlerPipeline`：

```csharp
public static partial class RequestContextHandlerPipeline
{
    public static HandlerPipeline<RequestContext> Instance { get; }
}
```

引用 `DesignPatterns.Extensions.DependencyInjection` 后，`{Context}HandlerPipeline` 额外生成：

```csharp
RequestContextHandlerPipeline.RegisterDi(services);
var pipeline = provider.GetRequiredService<HandlerPipeline<RequestContext>>();
```

`Create(IServiceProvider sp)` 按 `[HandlerOrder]` 顺序 `GetRequiredService` 各 handler。Singleton 管道在首次构建时固定 handler 实例；需要每次解析新管道时使用 `registryLifetime: ServiceLifetime.Transient`。

手动注册：`services.AddHandlerPipeline<TContext>(builder => ...)`（扩展包）。

## 诊断

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP005 | Error | 同一 context 下重复的 Order | ... |
| DP008 | Error | 标注 `[HandlerOrder]` 但未实现 `IHandler<TContext>` | ... |
| DP009 | Error | 标注 `[HandlerOrder]` 的 handler 缺少 public 无参构造 | ... |
| DP024 | Info | Analyzer：实现了 `IHandler<TContext>` 但未加 `[HandlerOrder]`（该 context 已有其它 handler 注册） | ... |
| DP024 | Info | CodeFix：一键添加 `[HandlerOrder(order, typeof(TContext))]` | ... |

## 不变量 / 兼容基线

1. Handler **调用** `await next(context, ct)` → 继续后续 handler；Handler **不调用** `next` → 后续 handler 不再执行（inbound 与 outbound 均跳过）——即**通过是否调用 `next` 实现短路**，不强制业务 context 实现额外接口。
2. 空管道：`InvokeAsync` / `InvokeTracedAsync` 正常完成，trace 为空。
3. `Use(null)` 抛 `ArgumentNullException`。

### 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）

## 实现概览

### 运行时

**Onion 调用模型**：先注册的 handler 先执行 inbound，后执行 outbound，与 ASP.NET Core 中间件一致。handler 通过 `HandlerDelegate<TContext>` 调用下一阶段，形成嵌套调用栈。

**异步一等**：所有 handler 与管道入口均使用 `ValueTask` + `CancellationToken`，避免不必要的 `Task` 分配并支持可取消的异步链。

**短路可观测性（`InvokeTracedAsync`）**：在不引入完整中间件框架的前提下，记录**每个已注册 handler** 在一次调用中的结局，便于调试鉴权拦截、早退逻辑等。

```csharp
var trace = await pipeline.InvokeTracedAsync(context, cancellationToken);

foreach (var step in trace.Steps)
{
    Console.WriteLine($"{step.Index}: {step.Name} → {step.Status}");
}

if (trace.Steps.Any(s => s.Status == HandlerPipelineStepStatus.NotReached))
{
    // 有 handler 因更早的短路而从未执行
}
```

**解读提示**：

- 区分「提前拦截」与「正常终端」：看**后续** handler 是否为 `NotReached`。例如鉴权 handler 不写 `401` 且不调用 `next` 时，其后 handler 应为 `NotReached`；而链末 handler 不调用 `next` 属于常见终端写法，其自身为 `ShortCircuited`，但**不代表**前面 handler 失败。

集成测试见 `tests/DesignPatterns.Tests/Integration/HandlerPipelineIntegrationTests.cs`（生成管道 + 鉴权通过 / 失败两条 trace 路径）。

### 源生成器

**`[HandlerOrder]` 排序**：`HandlerOrderGenerator` 扫描标注 `[HandlerOrder<TContext>(order)]` 或 `[HandlerOrder(order, typeof(TContext))]` 的 handler 类，按 `order` 数值升序排列（数值越小越先执行，与手动 `Use` 注册顺序一致）。同一 handler 类可标注多个 `[HandlerOrder<...>]`（`AllowMultiple = true`），分别加入不同 `TContext` 的生成管道。

**`{Context}HandlerPipeline` 生成**：对每种 `TContext` 生成 `public static partial class {Context}HandlerPipeline`，暴露 `Instance`（`HandlerPipeline<TContext>`）；引用 DI 扩展包后额外生成 `RegisterDi(services)` 与 `Create(IServiceProvider sp)`。

### 诊断检测细节

| ID | 说明 |
|----|------|
| DP005 | 同一 context 下重复的 Order |
| DP008 | 未实现 `IHandler<TContext>` |
| DP009 | 缺少 public 无参构造 |
| DP024 | Info \| Analyzer \| 实现了 `IHandler<TContext>` 但未加 `[HandlerOrder]`（该 context 已有其它 handler 注册） |
| DP024 | Info \| CodeFix \| 一键添加 `[HandlerOrder(order, typeof(TContext))]` |

DP024 属 **Analyzer**；DP005 / DP008 / DP009 属**生成器**。

## 设计权衡

### 选择了 onion 模型而非线性模型，因为...

与 ASP.NET Core 中间件一致的 onion 调用模型（先注册先执行 inbound，后执行 outbound）允许 handler 在 `next` 返回后执行后续逻辑（如日志后置、资源清理），线性模型只能单向传递、无法在「之后」做事。

### 短路语义：通过是否调用 `next` 实现

Handler **不调用** `next` 即短路，后续 handler（inbound 与 outbound 均跳过）不再执行。不强制业务 context 实现额外接口（如 `ICanShortCircuit`），保持 context 为纯数据载体。

### trace vs no-trace

`InvokeTracedAsync` 不改变执行顺序与短路语义，仅额外返回 `HandlerPipelineTrace`，便于调试；`InvokeAsync` 为零开销热路径。委托 handler 在 trace 中显示名为 `"<delegate>"`。

## 与生态的边界

| 模式 | 关注点 |
|------|--------|
| **Chain** | 请求在 **有序 pipeline** 中流动，可短路 |
| **Strategy** | 按 key **选一个** 实现 |
| **Factory** | 按 key **创建一个** 新实例 |

**DI 集成**：引用 `DesignPatterns.Extensions.DependencyInjection` 后，`{Context}HandlerPipeline` 生成 `RegisterDi(services)`；`Create(IServiceProvider sp)` 按 `[HandlerOrder]` 顺序 `GetRequiredService` 各 handler。Singleton 管道在首次构建时固定 handler 实例；需要每次解析新管道时使用 `registryLifetime: ServiceLifetime.Transient`。手动注册：`services.AddHandlerPipeline<TContext>(builder => ...)`（扩展包）。

## 已知局限

- **无 DI 自动注册**：Core 不引用 MSDI，handler 不会自动注册到容器；需引用 `DesignPatterns.Extensions.DependencyInjection` 并调用 `RegisterDi`，或手动 `AddHandlerPipeline<TContext>`。
- **无源生成器时需手动组装**：未使用 `[HandlerOrder]` 源生成器时，handler 须由调用方手动 `Use` 组装（见用法示例）。

```csharp
var pipeline = new HandlerPipelineBuilder<RequestContext>()
    .Use(new LoggingHandler())
    .Use(new AuthorizationHandler())   // 未授权时不调用 next → 短路
    .Use(new ResourceHandler())
    .Build();

await pipeline.InvokeAsync(new RequestContext("/api/orders", isAuthenticated: true));
```

示例见 [DesignPatterns.Samples.Chain](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Chain)：日志 → 鉴权 → 资源 handler，演示授权与未授权两种路径。

## 参考

- GoF: Chain of Responsibility (Behavioral)
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)
- [AGENTS.md](../../AGENTS.md)
