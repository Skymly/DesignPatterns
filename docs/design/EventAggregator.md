# Design Doc: Event Aggregator

> **关联 Spec**：[docs/spec/EventAggregator.md](../spec/EventAggregator.md)
> **关联 RFC**：[docs/rfc/XXX.md](../rfc/XXX.md)（如有）
> **关联 ADR**：ADR-XXX（如有）

## 概述

Event Aggregator 提供进程内、轻量的 **发布/订阅（pub/sub）** 机制：按事件类型路由到已订阅的处理器，无需引入完整消息总线框架。

## 设计目标

1. 最小 API：`Subscribe` / `Unsubscribe` / `PublishAsync`
2. 异步一等：`IEventHandler<T>.HandleAsync` 与 `CancellationToken`
3. 线程安全：订阅表在锁下修改，发布时对处理器列表做快照
4. 不侵入 Core 的 DI 依赖；可选 `AddEventAggregator` 扩展

## 实现概览

### 运行时

`EventAggregator` 内部以 `Dictionary<Type, List<object>>` 维护事件类型到处理器列表的映射。

- `Subscribe` / `Unsubscribe` 在内部锁上修改 `Dictionary<Type, List<object>>`。
- `PublishAsync` 在锁内复制当前处理器列表为 **快照**，随后在锁外调用，避免在 `HandleAsync` 执行期间持有锁。
- 同一 `handler` 实例可重复 `Subscribe`（会多次出现在列表中）；`Unsubscribe` 移除**第一次**匹配项。

#### 发布语义

- **顺序**：同一事件类型的处理器按 **订阅顺序** 依次调用。
- **并发**：`PublishAsync` 在快照列表上顺序 `await` 各 `HandleAsync`；不同事件类型互不影响。
- **空订阅**：无处理器时 `PublishAsync` 立即完成。
- **取消**：在调用每个处理器前检查 `cancellationToken`；处理器内也应协作取消。

#### DI 集成

独立包 `DesignPatterns.Extensions.DependencyInjection`：

```csharp
services.AddEventAggregator(); // 默认 Singleton
```

注册 `IEventAggregator` → `EventAggregator` 实例。处理器本身仍由应用注册到 DI 并手动 `Subscribe`，或由应用在启动时注入 `IEventAggregator` 后订阅。

### 源生成器

`RegisterEventHandlerGenerator`（增量源生成器，`IIncrementalGenerator` + `ForAttributeWithMetadataName`）在编译期扫描 `[RegisterEventHandler]` 特性：

1. **扫描**：收集所有标注 `[RegisterEventHandler]` 的 handler 类。
2. **按 event type 分组**：将 handler 按 `TEvent` 类型聚合。
3. **生成 `{Event}EventHandlerRegistry`**：`Event` 后缀自动剥离（如 `OrderPlacedEvent` → `OrderPlacedEventHandlerRegistry`）。
4. **静态路径 vs DI 路径**：
   - **静态路径**：`SubscribeAll(IEventAggregator)`，仅包含有公共无参构造的 handler，以 `new Handler()` + `Subscribe<TEvent>` 订阅。
   - **DI 路径**：`RegisterDi(IServiceCollection, ServiceLifetime)` 注册 handler 实现到容器；`SubscribeAll(IEventAggregator, IServiceProvider)` 从容器解析 handler + `Subscribe<TEvent>`。条件为 `DesignPatterns.EnableDiIntegration=true`。
   - 静态路径仅包含有公共无参构造的 handler；DI 路径包含全部有效 handler。这与其他生成器的无参构造约束一致（DP007/DP009/DP014/DP019/DP022 同源）。

### 诊断

| ID | 归属 | 语义 |
|----|------|------|
| DP044 | Analyzer | 实现 `IEventHandler<T>` 但未标注 `[RegisterEventHandler]`（Info） |
| DP045 | Generator | 同一 handler 类对同一 event type 重复标注 `[RegisterEventHandler]`（Error） |
| DP046 | Generator | 标注 `[RegisterEventHandler<T>]` 但类未实现 `IEventHandler<T>`（Error） |

## 设计权衡

### 线程安全：lock + 快照

`Subscribe` / `Unsubscribe` 在内部锁上修改订阅表；`PublishAsync` 在锁内复制处理器列表为快照，随后在锁外顺序 `await` 各 `HandleAsync`。这样避免在用户代码（`HandleAsync`）执行期间持有锁，降低死锁风险与锁竞争。

### 发布语义：顺序 await + 取消检查

`PublishAsync` 在快照列表上**顺序** `await` 各 `HandleAsync`（非并行扇出），保证处理器按订阅顺序执行且异常传播可预期。在调用每个处理器前检查 `cancellationToken`；处理器内也应协作取消。

### 静态路径 vs DI 路径

- **静态路径**（`SubscribeAll(IEventAggregator)`）仅包含有**公共无参构造**的 handler，以 `new Handler()` 实例化。这与其他生成器的无参构造约束一致（DP007/DP009/DP014/DP019/DP022 同源）。
- **DI 路径**（`RegisterDi` + `SubscribeAll(IEventAggregator, IServiceProvider)`）包含**全部有效 handler**，从容器解析实例。条件为 `DesignPatterns.EnableDiIntegration=true`。

### RegisterDi 默认 Transient

`RegisterDi` 默认 `implementationLifetime: ServiceLifetime.Transient`（每次解析新实例，因 handler 通常无状态）。调用方可显式传入其他 `ServiceLifetime`。

## 与生态的边界

> 本库以技术探索为目的，**允许与 MediatR 能力重叠**。下表用于说明当前实现的设计取向差异，供选型参考，**非**「不实现」的理由（见 [AGENTS.md](../../AGENTS.md)「项目是什么」）。

| | Event Aggregator | 典型 MediatR |
|---|---|---|
| 范围 | 进程内、同 AppDomain | 可扩展管道、行为、请求/通知模型 |
| 路由 | 按 `TEvent` 类型 | 按 Request/Notification 类型 + 管道 |
| 编译期 | `[RegisterEventHandler]` 源生成器 + DP044/045/046 诊断 | 通常基于约定或显式注册 |

## 已知局限

- 不做跨进程通信
- 不做持久化
- 不做重试策略
- 不做请求/响应关联 ID

以上为后续探索候选，准入标准见 [ROADMAP.md](../ROADMAP.md) F3。

## 参考

- 进程内 Observer / Mediator 变体（Behavioral）
- [AGENTS.md](../../AGENTS.md) — 项目规则与里程碑
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md) — 通用开发约定
- 示例：[DesignPatterns.Samples.EventAggregator](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.EventAggregator)
