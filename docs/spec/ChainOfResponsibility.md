# Spec: Chain of Responsibility

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/ChainOfResponsibility.md](../design/ChainOfResponsibility.md)
> **关联 ADR**：ADR-XXX（如有）

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

## 诊断 ID

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP005 | Error | 同一 context 下重复的 Order | ... |
| DP008 | Error | 标注 `[HandlerOrder]` 但未实现 `IHandler<TContext>` | ... |
| DP009 | Error | 标注 `[HandlerOrder]` 的 handler 缺少 public 无参构造 | ... |
| DP024 | Info | Analyzer：实现了 `IHandler<TContext>` 但未加 `[HandlerOrder]`（该 context 已有其它 handler 注册） | ... |
| DP024 | Info | CodeFix：一键添加 `[HandlerOrder(order, typeof(TContext))]` | ... |

## 不变量

1. Handler **调用** `await next(context, ct)` → 继续后续 handler；Handler **不调用** `next` → 后续 handler 不再执行（inbound 与 outbound 均跳过）——即**通过是否调用 `next` 实现短路**，不强制业务 context 实现额外接口。
2. 空管道：`InvokeAsync` / `InvokeTracedAsync` 正常完成，trace 为空。
3. `Use(null)` 抛 `ArgumentNullException`。

## 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）

## 不在范围内

- Autofac / DryIoc 扩展（可选，后续）
