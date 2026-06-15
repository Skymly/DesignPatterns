# State 转换表 — 设计与实现文档

## 概述

State 转换表为 **(当前状态, 触发器) → 下一状态** 的有限图提供轻量 primitive。本库提供：

- **运行时**：`ITransitionTable<TState,TTrigger>`、`TransitionTableBuilder`、`Transition()` 扩展
- **编译期**：`[StateMachine]` / `[Transition]` 源生成器 → `{StateEnum}TransitionTable` + partial holder 便捷方法

**不是**通用 UML 状态机框架（无层次状态、历史、持久化、entry/exit 动作 DSL）。需要完整状态机 DSL 时继续使用 Stateless 等库。

设计 RFC（已落地 v1）：[rfc/StateTransitionTable.md](rfc/StateTransitionTable.md)。

---

## 运行时 API（`DesignPatterns/Behavioral/`）

| 类型 | 职责 |
|------|------|
| `ITransitionTable<TState,TTrigger>` | 只读转换表：`TryTransition`、`GetAllowedTriggers`、`CanTransitionFrom` |
| `TransitionTableBuilder<TState,TTrigger>` | 手动注册边 + `WithInitial` |
| `TransitionTable<TState,TTrigger>` | 不可变实现 |
| `TransitionTableExtensions.Transition` | 非法边时抛 `InvalidTransitionException` |
| `InvalidTransitionException` | 含当前态与触发器信息 |

约束：`TState`、`TTrigger` 均为 **enum**；v1 不支持 string/int 键。

### 手动 Builder（无生成器时）

```csharp
using DesignPatterns.Behavioral;

public enum OrderStatus { Draft, Submitted, Paid }
public enum OrderTrigger { Submit, Pay }

var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
    .WithInitial(OrderStatus.Draft)
    .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)
    .Add(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)
    .Build();

if (table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out var next))
{
    // next == Submitted
}

// 非法边返回 false；Transition() 扩展抛 InvalidTransitionException
```

---

## 源生成器

### 用法

1. 定义 **state enum** 与 **trigger enum**（v1 须为两个独立 enum）。
2. 声明 **static partial** holder 类，标注 `[StateMachine(typeof(TState), typeof(TTrigger), Initial = ...)]`。
3. 在 holder 上标注一条或多条 `[Transition(from, trigger, to)]`。
4. 生成器输出 `{StateEnum}TransitionTable` 与 holder 上的 `TryTransition` / `InitialState` 等成员。

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
public static partial class OrderMachine;

// 生成：OrderStatusTransitionTable.Instance
// holder：OrderMachine.TryTransition(...)、OrderMachine.InitialState
```

### 命名规则

对 state enum `OrderStatus`：

| 生成物 | 名称 |
|--------|------|
| 转换表类型 | `OrderStatusTransitionTable` |
| 单例 | `OrderStatusTransitionTable.Instance` |
| Holder 便捷 API | `OrderMachine.TryTransition`、`InitialState` |

---

## 编译期诊断（DP026–DP031）

| ID | 级别 | 说明 |
|----|------|------|
| **DP026** | Error | 重复边 `(from, trigger)` |
| **DP027** | Error | `[Transition]` 的 state 非 enum 成员 |
| **DP028** | Error | trigger 非 enum 成员 |
| **DP029** | Error | `Initial` 非 state enum 成员 |
| **DP030** | Error | holder 非 static partial class |
| **DP031** | Info | state 从未作为 `from` 出现（终态提示） |

常量与文案：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](../DesignPatterns.Diagnostics/DiagnosticIds.cs)、[`DesignPatternsDiagnosticDescriptors.cs`](../DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs)。

用户向说明：[DesignPatterns.Docs — diagnostics](https://skymly.github.io/DesignPatterns.Docs/diagnostics#state-transition-table-dp026-dp031)。

---

## 非目标（v1）

- 层次 / 并发 / 历史状态
- Entry / exit / guard 编译期 DSL（副作用由 consumer 在 `TryTransition` 成功后自行处理）
- `string` / `int` 状态键
- DI 集成（v2 候选）

---

## 测试

| 层级 | 位置 |
|------|------|
| 运行时单元 | `tests/DesignPatterns.Tests/Behavioral/TransitionTableTests.cs` |
| 生成器集成 | `tests/DesignPatterns.Tests/Integration/StateTransitionIntegrationTests.cs` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs` |

## 示例

[DesignPatterns.Samples.State](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.State)
