# State 转换表 — 设计与实现文档

## 概述

State 转换表为 **(当前状态, 触发器) → 下一状态** 的有限图提供轻量 primitive。本库提供：

- **运行时**：`ITransitionTable<TState,TTrigger>`、`TransitionTableBuilder`、`Transition()` 扩展
- **编译期**：`[StateMachine]` / `[Transition]` 源生成器 → `{StateEnum}TransitionTable` + partial holder 便捷方法
- **v2 增强**：guard 委托（运行时 + 生成器）、DI 集成（`RegisterDi` 生成 + `AddTransitionTable` 扩展）、字面量边校验（DP036 Analyzer）

**不是**通用 UML 状态机框架（无层次状态、历史、持久化）。需要完整状态机 DSL 时继续使用 Stateless 等库。

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

## 编译期诊断（DP026–DP039）

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
| **DP037** | Error | 生成器 | entry/exit action 方法在 holder 类上未找到 |
| **DP038** | Error | 生成器 | entry/exit action 方法非 static（防御性；CS0708 先于生成器拒绝） |
| **DP039** | Error | 生成器 | entry/exit action 方法签名错误（须 `void Method(TState, TState, TTrigger)` 或 `ValueTask Method(TState, TState, TTrigger, CancellationToken)`） |

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

## v2：Entry / Exit Actions

v2 为转换边增加可选的 **entry/exit action** 委托，在 `TryTransitionAsync` 成功时按序调用。Action 仅通过 async 路径（`TryTransitionAsync`）触发；同步 `TryTransition` 不调用 action。

### 运行时 API

`TransitionTableBuilder.Add` 提供 `onEnterSync` / `onExitSync` / `onEnterAsync` / `onExitAsync` 参数：

```csharp
var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
    .WithInitial(OrderStatus.Draft)
    .Add(
        OrderStatus.Draft,
        OrderTrigger.Submit,
        OrderStatus.Submitted,
        guard: null,
        onEnterSync: (from, to, trigger) => Console.WriteLine($"Entering {to}"),
        onExitSync: (from, to, trigger) => Console.WriteLine($"Exiting {from}"))
    .Build();
```

执行顺序（`TryTransitionAsync`）：guard → OnExit（sync → async）→ OnEnter（sync → async）→ 返回结果。

> **部分执行**：若 OnExit 成功后 OnEnter 抛异常，异常传播给调用方，OnExit 副作用已发生。调用方需自行处理补偿。`TransitionResult<TState>` 不暴露执行进度。

### 源生成器

`[Transition]` 特性新增 `OnEnter` / `OnExit` 命名属性，指定 holder 类上的 **static** 方法名：

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
    OnEnter = nameof(OnSubmitted), OnExit = nameof(OnLeaveDraft))]
public static partial class OrderMachine
{
    public static void OnSubmitted(OrderStatus from, OrderStatus to, OrderTrigger trigger) { }
    public static void OnLeaveDraft(OrderStatus from, OrderStatus to, OrderTrigger trigger) { }
}
```

Action 方法须满足：
- **static**（DP038）
- 签名 `void Method(TState from, TState to, TTrigger trigger)`（sync）或 `ValueTask Method(TState from, TState to, TTrigger trigger, CancellationToken)`（async）（DP039）
- 在 holder 类上声明（DP037）

---

## v2：IStateMachine 实例包装器

`IStateMachine<TState, TTrigger>` 是 `ITransitionTable` 的有状态包装，自动跟踪 `CurrentState` 并在每次成功转换后更新。

### 运行时 API

```csharp
var machine = new StateMachine<OrderStatus, OrderTrigger>(table);
// machine.CurrentState == table.InitialState

if (machine.TryTransition(OrderTrigger.Submit, out var next))
{
    // machine.CurrentState == OrderStatus.Submitted
}
```

`TryTransition` 委托给底层 table；成功时更新 `CurrentState`。`TryTransitionAsync` 同理，并触发 entry/exit action。

> **线程安全**：`StateMachine<TState,TTrigger>` 非线程安全，设计为单线程使用。多线程场景应在调用方同步，或每线程使用独立实例。

---

## 非目标（v1/v2）

- ~~层次 / 并发 / 历史状态~~ — **层次状态已在 v3 实现**（见下方「层次状态机（v3）」章节）；并发 / 历史状态仍为非目标
- `string` / `int` 状态键
- guard 的编译期表达式求值（v2 仅运行时委托 + 方法引用校验）

---

## 层次状态机（v3）

v3 引入层次状态机（Hierarchical State Machine），支持父子状态关系、边继承展平、entry/exit action 链合成与 `IStateHierarchy<TState>` DI 集成。设计 RFC：[rfc/HierarchicalStateMachine.md](rfc/HierarchicalStateMachine.md) §7–§8。

### v3 交付阶段

| 阶段 | 内容 | 状态 |
|------|------|------|
| **v3.1** | 运行时 `IStateHierarchy<TState>`、`TransitionTableBuilder.WithParent` | 已完成 |
| **v3.2** | 源生成器 `[StateParent]` 收集、边继承展平、诊断 DP056–DP059 | 已完成 |
| **v3.3** | LCA 算法 + entry/exit action 链合成 + 复合委托生成 | 已完成 |
| **v3.4** | DI 集成（`AddStateHierarchy` + 生成器 `RegisterDi` 注册 `IStateHierarchy`）+ 示例 + 文档 | 已完成 |

### 启用层次模式

在 `[StateMachine]` 特性上设置 `Hierarchical = true`，并用 `[StateParent]` 声明父子关系：

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft, Hierarchical = true)]
[StateParent(OrderStatus.Submitted, OrderStatus.Active)]
[StateParent(OrderStatus.Paid, OrderStatus.Active)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
[Transition(OrderStatus.Active, OrderTrigger.Cancel, OrderStatus.Cancelled, OnExit = nameof(OnExitActive))]
public static partial class OrderMachine
{
    public static void OnExitActive(OrderStatus from, OrderStatus to, OrderTrigger trigger) { }
}
```

### 边继承展平（v3.2）

`[StateParent]` 声明的父子关系在编译期被 `HierarchyFlattener` 展平：父状态上的边自动继承到所有子状态。上例中 `Active + Cancel → Cancelled` 会被展平为 `Submitted + Cancel → Cancelled` 和 `Paid + Cancel → Cancelled`，无需重复声明。

诊断 DP056–DP059 覆盖：循环父链、自引用父、未知子/父状态、孤立父声明。

### Entry/Exit Action 链合成（v3.3）

当层次模式下一条展平后的边跨越多个层级时，源生成器合成复合委托（composite delegate），按 RFC §8 的 LCA（最低公共祖先）算法计算 exit/enter 链：

- **Exit 链**：从 `from` 向上到 LCA（不含 LCA），按从子到父的顺序执行
- **Enter 链**：从 LCA 向下到 `to`（不含 LCA），按从父到子的顺序执行
- **复合委托**：仅当链有 2+ action 时生成 `CompositeExit_{From}_{Trigger}` / `CompositeEnter_{From}_{Trigger}` 静态方法；1 action 直接用原引用；0 action 用 null

**边界场景（RFC §8.4）**：

| 场景 | Exit 链 | Enter 链 |
|------|---------|----------|
| 自环（from == to） | `[from]` | `[from]` |
| LCA == from（进入后代） | `[]`（空） | LCA → to 的子链 |
| LCA == to（返回祖先） | from → LCA 的子链 | `[]`（空） |
| 无公共祖先 | from 全祖链 | to 全祖链 |

复合委托对运行时透明：`TransitionEdge` 结构不变，复合委托是单个 `Action<TState,TState,TTrigger>` 或 `Func<...,ValueTask>`。

### `IStateHierarchy<TState>` DI 集成（v3.4）

生成的 `OrderStatusTransitionTable` 同时实现 `ITransitionTable<TState,TTrigger>` 和 `IStateHierarchy<TState>`。当启用 DI 集成时，生成的 `RegisterDi` 方法自动注册两者：

```csharp
var services = new ServiceCollection();
OrderStatusTransitionTable.RegisterDi(services); // 注册 ITransitionTable + IStateHierarchy
services.AddStateMachine<OrderStatus, OrderTrigger>();

var provider = services.BuildServiceProvider();
var table = provider.GetRequiredService<ITransitionTable<OrderStatus, OrderTrigger>>();
var hierarchy = provider.GetRequiredService<IStateHierarchy<OrderStatus>>();
```

手动注册时使用 `AddStateHierarchy<TState, TTrigger>` 扩展方法（从容器解析 table 并转型）。

### `TransitionTableBuilder.Add` 重载简化

v3.4 将 `Add` 的多个重载合并为单一签名，所有可选参数（`guard`、`onEnterSync`、`onExitSync`、`onEnterAsync`、`onExitAsync`）均有默认值 `null`，调用方可按命名参数传任意子集，无需为占位提供 `null`：

```csharp
// v3.4：直接用命名参数，无需 guard 占位
.Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, onExitSync: OnExitDraft)

// v3.4 之前：需要 guard 占位
.Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, guard: null, onEnterSync: null, onExitSync: OnExitDraft)
```

---

## 测试

| 层级 | 位置 |
|------|------|
| 运行时单元 | `tests/DesignPatterns.Tests/Behavioral/TransitionTableTests.cs` |
| 生成器集成 | `tests/DesignPatterns.Tests/Integration/StateTransitionIntegrationTests.cs` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs` |
| Guard 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs`（guard 用例） |
| Action 链合成单元 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/ActionChainComposerTests.cs` |
| 层次 action 链快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs`（`EmitsHierarchicalTableWith*ActionChain`） |
| 层次 DI 快照 | `tests/DesignPatterns.SourceGenerators.Tests/Generators/StateTransitionGeneratorTests.cs`（`EmitsHierarchicalTableWithDiIntegrationRegistersIStateHierarchy`） |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests/TransitionTableDiExtensionsTests.cs` |
| 字面量边 Analyzer | `tests/DesignPatterns.Analyzers.Tests/StateTransitionLiteralEdgeAnalyzerTests.cs` |

## 示例

- [DesignPatterns.Samples.State](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.State) — 基础状态机（guard、entry/exit action、DI）
- [DesignPatterns.Samples.HierarchicalState](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.HierarchicalState) — 层次状态机（`[StateParent]`、边继承展平、action 链合成、`IStateHierarchy` DI）
