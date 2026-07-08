# Spec: Event Aggregator

> **版本**：v0.2.2
> **关联 Design Doc**：[docs/design/EventAggregator.md](../design/EventAggregator.md)
> **关联 ADR**：ADR-XXX（如有）

## API 面

### 运行时接口

| 类型 | 职责 |
|------|------|
| `IEventHandler<TEvent>` | 处理指定事件类型 |
| `IEventAggregator` | 订阅、取消订阅、发布 |
| `EventAggregator` | 默认实现 |

#### 事件处理器

```csharp
public interface IEventHandler<in TEvent>
{
    ValueTask HandleAsync(TEvent evt, CancellationToken cancellationToken = default);
}
```

任意类实现该接口即可作为处理器；**不要求**继承基类或注册特性。

#### 聚合器

```csharp
public interface IEventAggregator
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler);
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler);
    ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);
}
```

事件类型 `TEvent` 可以是 `class`、`struct` 或 `record`；同一聚合器上可并存多种 `TEvent`。

#### 基本用法

```csharp
var aggregator = new EventAggregator();

aggregator.Subscribe(new EmailNotificationHandler());
aggregator.Subscribe(new AuditLogHandler());

await aggregator.PublishAsync(new OrderPlacedEvent("ORD-001", 99.99m));

aggregator.Unsubscribe(auditHandler);
await aggregator.PublishAsync(new OrderPlacedEvent("ORD-002", 49.99m));
```

### 特性（Attribute）

| 特性 | 形态 | 适用 TFM |
|------|------|----------|
| `[RegisterEventHandler(typeof(FooEvent))]` | 非泛型 | 全部（netstandard2.0+） |
| `[RegisterEventHandler<FooEvent>]` | 泛型 | `#if NET7_0_OR_GREATER`（C# 11+ generic attributes） |

与 `[RegisterStrategy]` / `[RegisterFactory]` 的双形态模式一致。

### 生成器产出

`RegisterEventHandlerGenerator` 在编译期扫描 `[RegisterEventHandler]` 特性，按 **event type 分组** 生成 `{Event}EventHandlerRegistry` 静态类（`Event` 后缀自动剥离，如 `OrderPlacedEvent` → `OrderPlacedEventHandlerRegistry`）：

| 成员 | 路径 | 条件 |
|------|------|------|
| `SubscribeAll(IEventAggregator)` | 静态：`new Handler()` + `Subscribe<TEvent>` | handler 有公共无参构造 |
| `RegisterDi(IServiceCollection, ServiceLifetime)` | DI：注册 handler 实现到容器 | `DesignPatterns_EnableDiIntegration=true` |
| `SubscribeAll(IEventAggregator, IServiceProvider)` | DI：从容器解析 handler + `Subscribe<TEvent>` | `DesignPatterns_EnableDiIntegration=true` |

**静态路径**仅包含有公共无参构造的 handler；**DI 路径**包含全部有效 handler。这与其他生成器的无参构造约束一致（DP007/DP009/DP014/DP019/DP022 同源）。

#### 静态路径（无 DI 依赖）

```csharp
public record OrderPlacedEvent(string OrderId);

[RegisterEventHandler<OrderPlacedEvent>]
public sealed class LogOrderHandler : IEventHandler<OrderPlacedEvent>
{
    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken ct = default) => default;
}

// 启动时
var aggregator = new EventAggregator();
OrderPlacedEventHandlerRegistry.SubscribeAll(aggregator);
await aggregator.PublishAsync(new OrderPlacedEvent("ORD-001"));
```

#### DI 路径（两步）

```csharp
// 1. 注册聚合器 + handler 实现
services.AddEventAggregator();
OrderPlacedEventHandlerRegistry.RegisterDi(services);

// 2. 启动时从容器解析并订阅
var provider = services.BuildServiceProvider();
var aggregator = provider.GetRequiredService<IEventAggregator>();
OrderPlacedEventHandlerRegistry.SubscribeAll(aggregator, provider);
```

`RegisterDi` 默认 `implementationLifetime: ServiceLifetime.Transient`（每次解析新实例，因 handler 通常无状态）。

## 诊断 ID

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP044 | Info | 实现 `IEventHandler<T>` 但未标注 `[RegisterEventHandler]` | 提示未注册的 EventHandler |
| DP045 | Error | 同一 handler 类对同一 event type 重复标注 `[RegisterEventHandler]` | 报告重复标注 |
| DP046 | Error | 标注 `[RegisterEventHandler<T>]` 但类未实现 `IEventHandler<T>` | 报告契约不匹配 |

## 不变量

1. 同一事件类型的处理器按 **订阅顺序** 依次调用。
2. `PublishAsync` 在发布前对处理器列表做 **快照**，随后在锁外调用，避免在 `HandleAsync` 执行期间持有锁。
3. 同一 `handler` 实例可重复 `Subscribe`（会多次出现在列表中）。
4. `Unsubscribe` 移除**第一次**匹配项。
5. 无处理器时 `PublishAsync` 立即完成（空订阅）。

## 兼容基线

- netstandard2.0 / net8.0（运行时核心，两者均须可用并随包分发）
- DI 集成：独立包 `DesignPatterns.Extensions.DependencyInjection`，`services.AddEventAggregator()` 注册 `IEventAggregator` → `EventAggregator`（默认 Singleton）

## 不在范围内

- 不做弱事件 / 委托语法糖包装
- 不做按名称或 topic 字符串路由（仅按 CLR 类型）
- 不在 Core 中引用 DI 容器
- 不用反射扫描程序集自动订阅（改由 `[RegisterEventHandler]` 源生成器在编译期收集 handler）
