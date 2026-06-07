# Event Aggregator — 设计与实现文档

## 概述

Event Aggregator 提供进程内、轻量的 **发布/订阅（pub/sub）** 机制：按事件类型路由到已订阅的处理器，无需引入完整消息总线框架。

## 设计目标

1. 最小 API：`Subscribe` / `Unsubscribe` / `PublishAsync`
2. 异步一等：`IEventHandler<T>.HandleAsync` 与 `CancellationToken`
3. 线程安全：订阅表在锁下修改，发布时对处理器列表做快照
4. 不侵入 Core 的 DI 依赖；可选 `AddEventAggregator` 扩展

---

## 运行时 API（`DesignPatterns/Behavioral/`）

| 类型 | 职责 |
|------|------|
| `IEventHandler<TEvent>` | 处理指定事件类型 |
| `IEventAggregator` | 订阅、取消订阅、发布 |
| `EventAggregator` | 默认实现 |

### 事件处理器

```csharp
public interface IEventHandler<in TEvent>
{
    ValueTask HandleAsync(TEvent evt, CancellationToken cancellationToken = default);
}
```

任意类实现该接口即可作为处理器；**不要求**继承基类或注册特性。

### 聚合器

```csharp
public interface IEventAggregator
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler);
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler);
    ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);
}
```

### 基本用法

```csharp
var aggregator = new EventAggregator();

aggregator.Subscribe(new EmailNotificationHandler());
aggregator.Subscribe(new AuditLogHandler());

await aggregator.PublishAsync(new OrderPlacedEvent("ORD-001", 99.99m));

aggregator.Unsubscribe(auditHandler);
await aggregator.PublishAsync(new OrderPlacedEvent("ORD-002", 49.99m));
```

事件类型 `TEvent` 可以是 `class`、`struct` 或 `record`；同一聚合器上可并存多种 `TEvent`。

---

## 发布语义

- **顺序**：同一事件类型的处理器按 **订阅顺序** 依次调用。
- **并发**：`PublishAsync` 在快照列表上顺序 `await` 各 `HandleAsync`；不同事件类型互不影响。
- **空订阅**：无处理器时 `PublishAsync` 立即完成。
- **取消**：在调用每个处理器前检查 `cancellationToken`；处理器内也应协作取消。

---

## 线程安全

- `Subscribe` / `Unsubscribe` 在内部锁上修改 `Dictionary<Type, List<object>>`。
- `PublishAsync` 在锁内复制当前处理器列表为 **快照**，随后在锁外调用，避免在 `HandleAsync` 执行期间持有锁。
- 同一 `handler` 实例可重复 `Subscribe`（会多次出现在列表中）；`Unsubscribe` 移除**第一次**匹配项。

---

## DI 集成

独立包 `DesignPatterns.Extensions.DependencyInjection`：

```csharp
services.AddEventAggregator(); // 默认 Singleton
```

注册 `IEventAggregator` → `EventAggregator` 实例。处理器本身仍由应用注册到 DI 并手动 `Subscribe`，或由应用在启动时注入 `IEventAggregator` 后订阅。

---

## 与 MediatR / 消息总线的边界

| | Event Aggregator | 典型 MediatR |
|---|---|---|
| 范围 | 进程内、同 AppDomain | 可扩展管道、行为、请求/通知模型 |
| 路由 | 按 `TEvent` 类型 | 按 Request/Notification 类型 + 管道 |
| 编译期 | 无源生成器、无 DP 诊断 | 通常基于约定或显式注册 |

本库**不做** 跨进程、持久化、重试策略或请求/响应关联 ID。

---

## 示例

见 [DesignPatterns.Samples.EventAggregator](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.EventAggregator)。

---

## 不做

- 不做弱事件 / 委托语法糖包装
- 不做按名称或 topic 字符串路由（仅按 CLR 类型）
- 不在 Core 中引用 DI 容器
- 不用反射扫描程序集自动订阅

---

## 参考

- 进程内 Observer / Mediator 变体（Behavioral）
- [AGENTS.md](../AGENTS.md) — 项目规则与里程碑
- [docs/DEVELOPMENT.md](DEVELOPMENT.md) — 通用开发约定
