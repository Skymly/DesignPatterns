# Chain of Responsibility — 设计与实现文档

## 概述

责任链模式将请求沿 handler 链传递，每个 handler 决定是处理请求还是转发给下一个。本库提供 **中间件式管道 primitives**，不替代 MediatR 或 ASP.NET Core 完整管道框架。

## 设计目标

1. 与 ASP.NET Core 中间件一致的 **onion 调用模型**（先注册先执行 inbound）
2. 通过 **是否调用 `next`** 实现短路，不强制业务 context 实现额外接口
3. **异步一等**：`ValueTask` + `CancellationToken`
4. 不依赖 DI；handler 由调用方手动组装

## 运行时 API（`DesignPatterns/Behavioral/`）

| 类型 | 说明 |
|------|------|
| `HandlerDelegate<TContext>` | 调用链中下一阶段的委托 |
| `IHandler<TContext>` | 单个 handler：`InvokeAsync(context, next, ct)` |
| `HandlerPipelineBuilder<TContext>` | `Use(handler)` / `Use(delegate)`，`Build()` |
| `HandlerPipeline<TContext>` | 不可变管道，`InvokeAsync(context, ct)`、`InvokeTracedAsync`（见下文） |
| `HandlerPipelineTrace` | 一次 traced 调用的逐步结果 |
| `HandlerPipelineStep` | 单步：索引、handler 显示名、状态 |
| `HandlerPipelineStepStatus` | `Completed` / `ShortCircuited` / `NotReached` |

## 用法

```csharp
var pipeline = new HandlerPipelineBuilder<RequestContext>()
    .Use(new LoggingHandler())
    .Use(new AuthorizationHandler())   // 未授权时不调用 next → 短路
    .Use(new ResourceHandler())
    .Build();

await pipeline.InvokeAsync(new RequestContext("/api/orders", isAuthenticated: true));
```

### 短路语义

- Handler **调用** `await next(context, ct)` → 继续后续 handler
- Handler **不调用** `next` → 后续 handler 不再执行（inbound 与 outbound 均跳过）

### 边界行为

| 场景 | 行为 |
|------|------|
| 空管道 | `InvokeAsync` / `InvokeTracedAsync` 正常完成，trace 为空 |
| `Use(null)` | `ArgumentNullException` |

### 短路可观测性（`InvokeTracedAsync`）

F2 增强：在不引入完整中间件框架的前提下，记录**每个已注册 handler** 在一次调用中的结局，便于调试鉴权拦截、早退逻辑等。

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

| `HandlerPipelineStepStatus` | 含义 |
|----------------------------|------|
| **Completed** | Handler 已执行且调用了 `next` |
| **ShortCircuited** | Handler 已执行但**未**调用 `next`（管道在此 handler 之后不再向下） |
| **NotReached** | Handler **未执行**（因更早的 handler 短路） |

**与 `InvokeAsync` 的关系**：`InvokeTracedAsync` 不改变执行顺序与短路语义，仅额外返回 `HandlerPipelineTrace`；委托 handler 在 trace 中显示名为 `"<delegate>"`。

**解读提示**：

- 区分「提前拦截」与「正常终端」：看**后续** handler 是否为 `NotReached`。例如鉴权 handler 不写 `401` 且不调用 `next` 时，其后 handler 应为 `NotReached`；而链末 handler 不调用 `next` 属于常见终端写法，其自身为 `ShortCircuited`，但**不代表**前面 handler 失败。
- `HandlerPipelineTrace.WasShortCircuited` 为「是否存在任一步为 `ShortCircuited`」；终端 handler 不调用 `next` 时也会为 `true`，不宜单独作为「业务短路」判据。

集成测试见 `tests/DesignPatterns.Tests/Integration/HandlerPipelineIntegrationTests.cs`（生成管道 + 鉴权通过 / 失败两条 trace 路径）。

## 与 Strategy / Factory 的边界

| 模式 | 关注点 |
|------|--------|
| **Chain** | 请求在 **有序 pipeline** 中流动，可短路 |
| **Strategy** | 按 key **选一个** 实现 |
| **Factory** | 按 key **创建一个** 新实例 |

## 示例

见 [DesignPatterns.Samples.Chain](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Chain)：日志 → 鉴权 → 资源 handler，演示授权与未授权两种路径。

## 编译期：`[HandlerOrder]` 源生成器

### 特性

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

### 生成输出

对每种 `TContext` 生成 `{Context}HandlerPipeline`：

```csharp
public static partial class RequestContextHandlerPipeline
{
    public static HandlerPipeline<RequestContext> Instance { get; }
}
```

### 诊断

| ID | 说明 |
|----|------|
| DP005 | 同一 context 下重复的 Order |
| DP008 | 未实现 `IHandler<TContext>` |
| DP009 | 缺少 public 无参构造 |
| DP024 | Info | Analyzer | 实现了 `IHandler<TContext>` 但未加 `[HandlerOrder]`（该 context 已有其它 handler 注册） |
| DP024 | Info | CodeFix | 一键添加 `[HandlerOrder(order, typeof(TContext))]` |

## DI 集成

引用 `DesignPatterns.Extensions.DependencyInjection` 后，`{Context}HandlerPipeline` 生成：

```csharp
RequestContextHandlerPipeline.RegisterDi(services);
var pipeline = provider.GetRequiredService<HandlerPipeline<RequestContext>>();
```

`Create(IServiceProvider sp)` 按 `[HandlerOrder]` 顺序 `GetRequiredService` 各 handler。Singleton 管道在首次构建时固定 handler 实例；需要每次解析新管道时使用 `registryLifetime: ServiceLifetime.Transient`。

手动注册：`services.AddHandlerPipeline<TContext>(builder => ...)`（扩展包）。

## 后续

- Autofac / DryIoc 扩展（可选）

## 参考

- GoF: Chain of Responsibility (Behavioral)
- [docs/DEVELOPMENT.md](DEVELOPMENT.md)
- [AGENTS.md](../AGENTS.md)
