# State 转换表 — 设计与实现文档

## 概述

State 转换表为 **(当前状态, 触发器) → 下一状态** 的有限图提供轻量 primitive。本库提供：

- **运行时**：`ITransitionTable<TState,TTrigger>`、`TransitionTableBuilder`、`Transition()` 扩展
- **编译期**：`[StateMachine]` / `[Transition]` 源生成器 → `{StateEnum}TransitionTable` + partial holder 便捷方法
- **v2 增强**：guard 委托（运行时 + 生成器）、DI 集成（`RegisterDi` 生成 + `AddTransitionTable` 扩展）、字面量边校验（DP036 Analyzer）

**不是**通用 UML 状态机框架（无层次状态、历史、持久化、entry/exit 动作 DSL）。需要完整状态机 DSL 时继续使用 Stateless 等库。

设计 RFC：[rfc/StateTransitionTable.md](rfc/StateTransitionTable.md)（v1 已落地，v2 guard/DI/DP036 已实现）。

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

## 编译期诊断（DP026–DP036）

| ID | 级别 | 归属 | 说明 |
|----|------|------|------|
| **DP026** | Error | 生成器 | 重复边 `(from, trigger)` |
| **DP027** | Error | 生成器 | `[Transition]` 的 state 非 enum 成员 |
| **DP028** | Error | 生成器 | trigger 非 enum 成员 |
| **DP029** | Error | 生成器 | `Initial` 非 state enum 成员 |
| **DP030** | Error | 生成器 | holder 非 static partial class |
| **DP031** | Info | 生成器 | state 从未作为 `from` 出现（终态提示） |
| **DP032** | Error | 生成器 | guard 方法在 holder 类上未找到 |
| **DP034** | Error | 生成器 | guard 方法非 static |
| **DP035** | Error | 生成器 | guard 方法签名错误（须 `bool Method(TState, TTrigger)`） |
| **DP036** | Info | Analyzer | `TryTransition` 字面量 (state, trigger) 对未声明（与 DP025 对称） |

常量与文案：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](../DesignPatterns.Diagnostics/DiagnosticIds.cs)、[`DesignPatternsDiagnosticDescriptors.cs`](../DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs)。

用户向说明：[DesignPatterns.Docs — diagnostics](https://skymly.github.io/DesignPatterns.Docs/diagnostics#state-transition-table-dp026-dp031)。

---

## v2：Guard 委托

v2 为转换边增加可选的 **guard 委托**，在 `TryTransition` 返回目标状态前求值；guard 返回 `false` 时该次转换视为不存在。

### 运行时 API

`TransitionTableBuilder.Add` 提供 guard 重载：

```csharp
var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
    .WithInitial(OrderStatus.Draft)
    .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
         guard: (state, trigger) => !string.IsNullOrEmpty(orderId))
    .Build();
```

`TransitionEdge<TState, TTrigger>.Guard` 存储委托；`TransitionTable.TryTransition` 在命中边后调用 guard。

### 源生成器

`[Transition]` 特性新增 `Guard` 命名属性，指定 holder 类上的 **static** 方法名：

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, Guard = nameof(CanSubmit))]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
public static partial class OrderMachine
{
    public static bool CanSubmit(OrderStatus state, OrderTrigger trigger) => true;
}
```

生成器将 guard 方法引用编译为委托并嵌入生成的转换表。guard 方法须满足：

- **static**（DP034）
- 签名 `bool Method(TState, TTrigger)`（DP035）
- 在 holder 类上声明（DP032）

---

## v2：DI 集成

### 生成的 `RegisterDi` 方法

当消费项目引用 `DesignPatterns.Extensions.DependencyInjection`（自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`）时，生成器在转换表类上额外输出：

```csharp
public static IServiceCollection RegisterDi(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Singleton)
{
    services.TryAdd(new ServiceDescriptor(
        typeof(ITransitionTable<OrderStatus, OrderTrigger>),
        _ => Instance,
        lifetime));
    return services;
}
```

调用方式：

```csharp
OrderStatusTransitionTable.RegisterDi(services);
// 或链式
services.AddOtherStuff().RegisterDi<OrderStatus, OrderTrigger>();
```

启用开关：MSBuild 属性 `DesignPatterns_EnableDiIntegration`（引用 DI 扩展包时自动为 `true`）。

### 手动注册扩展

`DesignPatterns.Extensions.DependencyInjection.DesignPatternsServiceCollectionExtensions` 提供 `AddTransitionTable` 扩展方法，用于注册预构建的表实例：

```csharp
services.AddTransitionTable(manualTable);
services.AddTransitionTable(OrderStatusTransitionTable.Instance, ServiceLifetime.Singleton);
```

使用 `TryAdd` 语义，重复注册不会覆盖。

---

## v2：字面量边校验（DP036）

`StateTransitionLiteralEdgeAnalyzer`（`DesignPatterns.Analyzers`）在编译期扫描所有程序集的 `[StateMachine]` + `[Transition]` 特性，构建有效边集合，然后校验 `TryTransition` 调用的字面量 (state, trigger) 参数对：

```csharp
// DP036: (Cancelled, Submit) 未声明
table.TryTransition(OrderStatus.Cancelled, OrderTrigger.Submit, out _);
```

- 严重级别 **Info**（与 DP025 一致；运行时 `TryTransition` 仅返回 `false`，非错误）
- 跨程序集支持（表可在引用程序集中声明）
- 非常量参数跳过（运行时计算的值无法编译期校验）
- 仅校验 `TryTransition`（`CanTransitionFrom` 只接收 state，无 trigger 可校验）

---

## 非目标（v1/v2）

- 层次 / 并发 / 历史状态
- Entry / exit 动作 DSL（副作用由 consumer 在 `TryTransition` 成功后自行处理）
- `string` / `int` 状态键
- guard 的编译期表达式求值（v2 仅运行时委托 + 方法引用校验）

---

## 测试

| 层级 | 位置 |
|------|------|
| 运行时单元 | `tests/DesignPatterns.Tests/Behavioral/TransitionTableTests.cs` |
| 生成器集成 | `tests/DesignPatterns.Tests/Integration/StateTransitionIntegrationTests.cs` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs` |
| Guard 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs`（guard 用例） |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests/TransitionTableDiExtensionsTests.cs` |
| 字面量边 Analyzer | `tests/DesignPatterns.Analyzers.Tests/StateTransitionLiteralEdgeAnalyzerTests.cs` |

## 示例

[DesignPatterns.Samples.State](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.State)
