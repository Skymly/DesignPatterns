# Design Doc: State 转换表

> **版本**：v0.2.3-preview1（与 NuGet 包版本对齐）
> **关联 ADR**：[ADR-005](../adr/ADR-005-state-transition-table.md)

## 概述

State 转换表为 **(当前状态, 触发器) → 下一状态** 的有限图提供轻量 primitive。本库提供：

- **运行时**：`ITransitionTable<TState,TTrigger>`、`TransitionTableBuilder`、`Transition()` 扩展
- **编译期**：`[StateMachine]` / `[Transition]` 源生成器 → `{StateEnum}TransitionTable` + partial holder 便捷方法
- **v2 增强**：guard 委托（运行时 + 生成器）、DI 集成（`RegisterDi` 生成 + `AddTransitionTable` 扩展）、字面量边校验（DP036 Analyzer）
- **v3 增强**：层次状态机（`[StateParent]` + 编译期展平 + entry/exit action 链合成 + `IStateHierarchy` DI 集成）

**不是**通用 UML 状态机框架（无并发状态、历史状态、持久化）。需要完整状态机 DSL 时继续使用 Stateless 等库。

## 设计目标

1. 提供与 Strategy/Factory/Chain 同级的轻量状态机 primitive
2. enum 状态 + enum 触发器 + 编译期生成冻结查找表，运行时零分配查找
3. 声明式标注 + 生成胶水 + 可选手动 Builder，不侵入实现类的继承链
4. 编译期诊断拦截非法转换、重复边、guard/action 签名错误
5. 层次状态机（v3）通过编译期展平实现，运行时无层级开销
6. 同时支持同步与异步路径（entry/exit action 仅 async 路径触发）

## API 面

### 运行时接口

命名空间：`DesignPatterns.Behavioral`

#### 转换表

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// 只读转换表：(当前状态, 触发器) → 下一状态。
/// </summary>
public interface ITransitionTable<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    bool TryTransition(TState from, TTrigger trigger, out TState to);
    bool CanTransitionFrom(TState from, TTrigger trigger);
    IReadOnlyCollection<TTrigger> GetAllowedTriggers(TState from);
    TState InitialState { get; }
}
```

- `TryTransition`：命中边且 guard 通过时返回 `true` 并设置 `to`；否则返回 `false`。
- `GetAllowedTriggers`：返回从 `from` 出发的所有合法触发器。
- `CanTransitionFrom`：仅接收 state（无 trigger），判断是否存在以 `from` 为起点的边。

`TransitionTable<TState, TTrigger>` 为不可变实现；net8.0 上内部使用 `FrozenDictionary` 优化查找。

#### Builder（手动注册，无生成器时）

```csharp
public sealed class TransitionTableBuilder<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TransitionTableBuilder<TState, TTrigger> WithInitial(TState initial);
    public TransitionTableBuilder<TState, TTrigger> Add(
        TState from, TTrigger trigger, TState to,
        Func<TState, TTrigger, bool>? guard = null,
        Action<TState, TState, TTrigger>? onEnterSync = null,
        Action<TState, TState, TTrigger>? onExitSync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onEnterAsync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onExitAsync = null);
    public ITransitionTable<TState, TTrigger> Build();
}
```

> **v3.4 重载简化**：`Add` 的多个重载合并为单一签名，所有可选参数（`guard`、`onEnterSync`、`onExitSync`、`onEnterAsync`、`onExitAsync`）均有默认值 `null`，调用方可按命名参数传任意子集，无需为占位提供 `null`。

手动 Builder 用法：

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

#### Transition 扩展

```csharp
public static class TransitionTableExtensions
{
    // 非法边时抛 InvalidTransitionException（含当前态与触发器信息）
    public static TState Transition<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table, TState from, TTrigger trigger)
        where TState : struct, Enum
        where TTrigger : struct, Enum;
}
```

#### Guard 委托

`TransitionTableBuilder.Add` 提供 `guard` 参数；`TransitionEdge<TState, TTrigger>.Guard` 存储委托。`TransitionTable.TryTransition` 在命中边后调用 guard；guard 返回 `false` 时该次转换视为不存在。

```csharp
var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
    .WithInitial(OrderStatus.Draft)
    .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
         guard: (state, trigger) => !string.IsNullOrEmpty(orderId))
    .Build();
```

#### Entry / Exit Actions

`TransitionTableBuilder.Add` 提供 `onEnterSync` / `onExitSync` / `onEnterAsync` / `onExitAsync` 参数。Action 仅通过 async 路径（`TryTransitionAsync`）触发；同步 `TryTransition` 不调用 action。

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

#### IStateMachine 实例包装器

```csharp
public interface IStateMachine<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    TState CurrentState { get; }
    bool TryTransition(TTrigger trigger, out TState to);
    ValueTask<TransitionResult<TState>> TryTransitionAsync(TTrigger trigger, CancellationToken ct = default);
}

public sealed class StateMachine<TState, TTrigger> : IStateMachine<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public StateMachine(ITransitionTable<TState, TTrigger> table);
    public TState CurrentState { get; } // 初始 == table.InitialState
    public bool TryTransition(TTrigger trigger, out TState to);
    public ValueTask<TransitionResult<TState>> TryTransitionAsync(TTrigger trigger, CancellationToken ct = default);
}
```

`TryTransition` 委托给底层 table；成功时更新 `CurrentState`。`TryTransitionAsync` 同理，并触发 entry/exit action。

```csharp
var machine = new StateMachine<OrderStatus, OrderTrigger>(table);
// machine.CurrentState == table.InitialState

if (machine.TryTransition(OrderTrigger.Submit, out var next))
{
    // machine.CurrentState == OrderStatus.Submitted
}
```

> **线程安全**：`StateMachine<TState,TTrigger>` 非线程安全，设计为单线程使用。多线程场景应在调用方同步，或每线程使用独立实例。

#### IStateHierarchy（v3 层次状态）

```csharp
public interface IStateHierarchy<TState>
    where TState : struct, Enum
{
    TState? GetParent(TState state);
    bool IsInState(TState state, TState ancestor);
    IReadOnlyList<TState> GetAncestors(TState state);
}
```

- `GetParent`：返回直接父状态，无父时返回 `null`。
- `IsInState`：判断 `state` 是否为 `ancestor` 的后代（含自身）。
- `GetAncestors`：从 `state` 向上到根的祖先链（不含 `state` 自身）。

### 特性（Attribute）

命名空间：`DesignPatterns.Behavioral`

#### [StateMachine]

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StateMachineAttribute : Attribute
{
    public Type State { get; }       // state enum 类型
    public Type Trigger { get; }     // trigger enum 类型
    public object? Initial { get; set; }  // 初始状态（state enum 成员）
    public bool Hierarchical { get; set; } // v3：启用层次状态模式
    public StateMachineAttribute(Type state, Type trigger);
}
```

#### [Transition]

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TransitionAttribute : Attribute
{
    public object From { get; }      // state enum 成员
    public object Trigger { get; }   // trigger enum 成员
    public object To { get; }        // state enum 成员
    public string? Guard { get; set; }    // holder 类上的 static guard 方法名
    public string? OnEnter { get; set; }  // holder 类上的 static entry action 方法名
    public string? OnExit { get; set; }   // holder 类上的 static exit action 方法名
    public TransitionAttribute(object from, object trigger, object to);
}
```

#### [StateParent]（v3 层次状态）

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class StateParentAttribute : Attribute
{
    public object Child { get; }  // state enum 成员
    public object Parent { get; } // state enum 成员
    public StateParentAttribute(object child, object parent);
}
```

#### 用法示例

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
public static partial class OrderMachine;

// 生成：OrderStatusTransitionTable.Instance
// holder：OrderMachine.TryTransition(...)、OrderMachine.InitialState
```

Guard + Entry/Exit 示例：

```csharp
[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, Guard = nameof(CanSubmit))]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid,
 OnEnter = nameof(OnSubmitted), OnExit = nameof(OnLeaveDraft))]
public static partial class OrderMachine
{
    public static bool CanSubmit(OrderStatus state, OrderTrigger trigger) => true;
    public static void OnSubmitted(OrderStatus from, OrderStatus to, OrderTrigger trigger) { }
    public static void OnLeaveDraft(OrderStatus from, OrderStatus to, OrderTrigger trigger) { }
}
```

层次状态示例（v3）：

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

### 生成器产出

对 state enum `OrderStatus`：

| 生成物 | 名称 |
|--------|------|
| 转换表类型 | `OrderStatusTransitionTable` |
| 单例 | `OrderStatusTransitionTable.Instance` |
| Holder 便捷 API | `OrderMachine.TryTransition`、`InitialState` |

#### {StateEnum}TransitionTable

```csharp
public sealed partial class OrderStatusTransitionTable
    : ITransitionTable<OrderStatus, OrderTrigger>
    , IStateHierarchy<OrderStatus>  // v3：层次模式时实现
{
    public static OrderStatusTransitionTable Instance { get; }
    public OrderStatus InitialState { get; }
    public bool TryTransition(OrderStatus from, OrderTrigger trigger, out OrderStatus to);
    public bool CanTransitionFrom(OrderStatus from, OrderTrigger trigger);
    public IReadOnlyCollection<OrderTrigger> GetAllowedTriggers(OrderStatus from);
    // v3 层次模式时额外实现 IStateHierarchy<OrderStatus>
    public OrderStatus? GetParent(OrderStatus state);
    public bool IsInState(OrderStatus state, OrderStatus ancestor);
    public IReadOnlyList<OrderStatus> GetAncestors(OrderStatus state);
}
```

#### Holder 便捷方法

生成器在 holder partial 类上输出：

- `TryTransition(...)` — 委托到 `Instance`
- `InitialState` — 委托到 `Instance.InitialState`

#### RegisterDi（引用 `DesignPatterns.Extensions.DependencyInjection` 时）

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
    // v3 层次模式时额外注册 IStateHierarchy<OrderStatus>
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

#### 手动注册扩展

`DesignPatterns.Extensions.DependencyInjection.DesignPatternsServiceCollectionExtensions` 提供 `AddTransitionTable` 扩展方法，用于注册预构建的表实例：

```csharp
services.AddTransitionTable(manualTable);
services.AddTransitionTable(OrderStatusTransitionTable.Instance, ServiceLifetime.Singleton);
```

使用 `TryAdd` 语义，重复注册不会覆盖。

v3 层次模式下使用 `AddStateHierarchy<TState, TTrigger>` 扩展方法（从容器解析 table 并转型）和 `AddStateMachine<TState, TTrigger>` 扩展方法。

## 诊断

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| **DP026** | Error | 重复边 `(from, trigger)` | 重复的转换边 |
| **DP027** | Error | `[Transition]` 的 state 非 enum 成员 | state 非 enum 成员 |
| **DP028** | Error | trigger 非 enum 成员 | trigger 非 enum 成员 |
| **DP029** | Error | `Initial` 非 state enum 成员 | Initial 非 state enum 成员 |
| **DP030** | Error | holder 非 static partial class | holder 须为 static partial class |
| **DP031** | Info | state 从未作为 `from` 出现（终态提示） | state 从未作为 from 出现 |
| **DP032** | Error | guard 方法在 holder 类上未找到 | guard 方法未找到 |
| **DP034** | Error | guard 方法非 static | guard 方法须为 static |
| **DP035** | Error | guard 方法签名错误（须 `bool Method(TState, TTrigger)`） | guard 方法签名错误 |
| **DP036** | Info | `TryTransition` 字面量 (state, trigger) 对未声明（与 DP025 对称） | 字面量边未声明 |
| **DP037** | Error | entry/exit action 方法在 holder 类上未找到 | action 方法未找到 |
| **DP038** | Error | entry/exit action 方法非 static（防御性；CS0708 先于生成器拒绝） | action 方法须为 static |
| **DP039** | Error | entry/exit action 方法签名错误（须 `void Method(TState, TState, TTrigger)` 或 `ValueTask Method(TState, TState, TTrigger, CancellationToken)`） | action 方法签名错误 |
| **DP056** | Error | `[StateParent]` 父链存在循环 | 父链循环 |
| **DP057** | Error | `[StateParent]` 的 child/parent 非 state enum 成员 | 非法 enum 成员 |
| **DP058** | Error | `[StateParent]` 自引用（child == parent） | 自引用父 |
| **DP059** | Error | `[StateParent]` 的 parent 从未作为任何边的 state 出现（孤立父声明） | 孤立父声明 |

常量与文案：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](../../DesignPatterns.Diagnostics/DiagnosticIds.cs)、[`DesignPatternsDiagnosticDescriptors.cs`](../../DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs)。

用户向说明：[DesignPatterns.Docs — diagnostics](https://skymly.github.io/DesignPatterns.Docs/diagnostics#state-transition-table-dp026-dp031)。

## 不变量 / 兼容基线

1. `TState` 和 `TTrigger` 均须为 **enum**（`where TState : struct, Enum`）。
2. Guard 方法签名须为 `bool Method(TState, TTrigger)`；须 **static**；须在 holder 类上声明。
3. Entry/Exit action 方法签名须为 `void Method(TState from, TState to, TTrigger trigger)`（sync）或 `ValueTask Method(TState from, TState to, TTrigger trigger, CancellationToken)`（async）；须 **static**；须在 holder 类上声明。
4. `StateMachine<TState, TTrigger>` **非线程安全**，设计为单线程使用。多线程场景应在调用方同步，或每线程使用独立实例。
5. 执行顺序（`TryTransitionAsync`）：guard → OnExit（sync → async）→ OnEnter（sync → async）→ 返回结果。
6. Entry/Exit action 仅通过 async 路径（`TryTransitionAsync`）触发；同步 `TryTransition` 不调用 action。
7. 部分执行语义：若 OnExit 成功后 OnEnter 抛异常，异常传播给调用方，OnExit 副作用已发生；`TransitionResult<TState>` 不暴露执行进度。
8. `TransitionTable<TState, TTrigger>` 不可变；`Build()` 后不可修改。
9. DI 注册使用 `TryAdd` 语义，重复注册不会覆盖。

### 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）
- 生成器实现：`IIncrementalGenerator` + `ForAttributeWithMetadataName`

## 实现概览

### 运行时

运行时核心位于 `DesignPatterns/Behavioral/`，提供以下关键类型：

- **`ITransitionTable<TState, TTrigger>`**：只读转换表接口，方法 `TryTransition`、`GetAllowedTriggers`、`CanTransitionFrom`，属性 `InitialState`。约束 `where TState : struct, Enum`、`where TTrigger : struct, Enum`。
- **`TransitionTable<TState, TTrigger>`**：不可变实现。net8.0 上内部使用 `FrozenDictionary` 优化查找；netstandard2.0 回退到普通 `Dictionary`。冻结查找表——`Build()` 后不可修改。
- **`TransitionTableBuilder<TState, TTrigger>`**：手动注册 Builder。`WithInitial` 设置初始状态；`Add` 注册边，支持可选的 `guard`、`onEnterSync`、`onExitSync`、`onEnterAsync`、`onExitAsync` 参数（v3.4 合并为单一签名，所有可选参数默认 `null`）。
- **`TransitionTableExtensions.Transition`**：扩展方法，非法边时抛 `InvalidTransitionException`（含当前态与触发器信息）。
- **`InvalidTransitionException`**：含当前态与触发器信息的异常类型。
- **`TransitionEdge<TState, TTrigger>`**：内部结构，存储 `Guard` 委托与 entry/exit action 委托。`TryTransition` 在命中边后调用 guard；guard 返回 `false` 时该次转换视为不存在。
- **`IStateMachine<TState, TTrigger>` / `StateMachine<TState, TTrigger>`**：有状态包装器，自动跟踪 `CurrentState` 并在每次成功转换后更新。`TryTransition` 委托给底层 table；`TryTransitionAsync` 同理并触发 entry/exit action。**非线程安全**，设计为单线程使用。
- **`TransitionResult<TState>`**：async 路径的返回类型，不暴露执行进度（部分执行语义见「设计权衡」）。
- **`IStateHierarchy<TState>`**（v3）：层次状态接口，方法 `GetParent`、`IsInState`、`GetAncestors`。生成的 `OrderStatusTransitionTable` 在层次模式下同时实现 `ITransitionTable` 和 `IStateHierarchy`。

执行顺序（`TryTransitionAsync`）：guard → OnExit（sync → async）→ OnEnter（sync → async）→ 返回结果。

### 源生成器

`StateTransitionGenerator` 为增量源生成器（`IIncrementalGenerator`），使用 `ForAttributeWithMetadataName` 管线：

- **`[StateMachine]` / `[Transition]` 扫描**：以 `[StateMachine]` 元数据名注册管线，收集 holder 类上的 `[Transition]` 特性。对每个 state enum 生成 `{StateEnum}TransitionTable` 类与 holder 上的便捷方法（`TryTransition`、`InitialState`）。
- **`{StateEnum}TransitionTable` 生成**：输出 `Instance` 静态单例属性、`InitialState` 属性、`TryTransition` / `CanTransitionFrom` / `GetAllowedTriggers` 方法。net8.0 上使用 `FrozenDictionary` 优化查找。
- **Guard 委托生成**：`[Transition]` 的 `Guard` 命名属性指定 holder 类上的 static 方法名。生成器将 guard 方法引用编译为委托并嵌入生成的转换表。校验 guard 方法须 static（DP034）、签名 `bool Method(TState, TTrigger)`（DP035）、在 holder 类上声明（DP032）。
- **Entry/Exit action 生成**：`[Transition]` 的 `OnEnter` / `OnExit` 命名属性指定 holder 类上的 static 方法名。校验 action 方法须 static（DP038）、签名 `void Method(TState, TState, TTrigger)` 或 `ValueTask Method(TState, TState, TTrigger, CancellationToken)`（DP039）、在 holder 类上声明（DP037）。
- **`[StateParent]` 收集**（v3.2）：收集 holder 类上的 `[StateParent]` 特性，构建父子关系图。诊断 DP056–DP059 覆盖：循环父链、自引用父、未知子/父状态、孤立父声明。
- **`HierarchyFlattener` 边继承展平**（v3.2）：`[StateParent]` 声明的父子关系在编译期被展平——父状态上的边自动继承到所有子状态。例如 `Active + Cancel → Cancelled` 会被展平为 `Submitted + Cancel → Cancelled` 和 `Paid + Cancel → Cancelled`，无需重复声明。
- **LCA 算法 + entry/exit action 链合成**（v3.3）：当层次模式下一条展平后的边跨越多个层级时，源生成器按 LCA（最低公共祖先）算法计算 exit/enter 链并合成复合委托（composite delegate）：
  - **Exit 链**：从 `from` 向上到 LCA（不含 LCA），按从子到父的顺序执行
  - **Enter 链**：从 LCA 向下到 `to`（不含 LCA），按从父到子的顺序执行
  - **复合委托**：仅当链有 2+ action 时生成 `CompositeExit_{From}_{Trigger}` / `CompositeEnter_{From}_{Trigger}` 静态方法；1 action 直接用原引用；0 action 用 null
  - 复合委托对运行时透明：`TransitionEdge` 结构不变，复合委托是单个 `Action<TState,TState,TTrigger>` 或 `Func<...,ValueTask>`

  **边界场景**：

  | 场景 | Exit 链 | Enter 链 |
  |------|---------|----------|
  | 自环（from == to） | `[from]` | `[from]` |
  | LCA == from（进入后代） | `[]`（空） | LCA → to 的子链 |
  | LCA == to（返回祖先） | from → LCA 的子链 | `[]`（空） |
  | 无公共祖先 | from 全祖链 | to 全祖链 |

- **DI 集成生成**：当消费项目引用 `DesignPatterns.Extensions.DependencyInjection`（自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`，设置 `DesignPatterns_EnableDiIntegration=true`）时，生成器在转换表类上额外输出 `RegisterDi` 方法。v3 层次模式下 `RegisterDi` 自动注册 `ITransitionTable` + `IStateHierarchy`。

### 诊断检测细节

#### 生成器诊断

| 检测点 | 逻辑 | 报告位置 |
|--------|------|----------|
| DP026（重复边） | 同一 `(from, trigger)` 对出现多次 | 重复的 `[Transition]` 特性 |
| DP027（state 非 enum 成员） | `[Transition]` 的 from/to 非 state enum 成员 | 特性参数位置 |
| DP028（trigger 非 enum 成员） | `[Transition]` 的 trigger 非 trigger enum 成员 | 特性参数位置 |
| DP029（Initial 非 enum 成员） | `Initial` 非 state enum 成员 | `Initial` 属性位置 |
| DP030（holder 非 static partial） | holder 类非 static partial class | 类声明 |
| DP031（终态提示） | state 从未作为 `from` 出现 | state enum 成员（Info） |
| DP032（guard 未找到） | guard 方法在 holder 类上未找到 | `Guard` 属性位置 |
| DP034（guard 非 static） | guard 方法非 static | `Guard` 属性位置 |
| DP035（guard 签名错误） | guard 方法签名非 `bool Method(TState, TTrigger)` | `Guard` 属性位置 |
| DP037（action 未找到） | entry/exit action 方法在 holder 类上未找到 | `OnEnter`/`OnExit` 属性位置 |
| DP038（action 非 static） | entry/exit action 方法非 static（防御性；CS0708 先于生成器拒绝 static 类中的实例成员，无法通过生成器测试触发） | `OnEnter`/`OnExit` 属性位置 |
| DP039（action 签名错误） | entry/exit action 方法签名非 `void Method(TState, TState, TTrigger)` 或 `ValueTask Method(TState, TState, TTrigger, CancellationToken)` | `OnEnter`/`OnExit` 属性位置 |
| DP056（父链循环） | `[StateParent]` 父链存在循环 | `[StateParent]` 特性 |
| DP057（非法 enum 成员） | `[StateParent]` 的 child/parent 非 state enum 成员 | 特性参数位置 |
| DP058（自引用父） | `[StateParent]` 的 child == parent | `[StateParent]` 特性 |
| DP059（孤立父声明） | `[StateParent]` 的 parent 从未作为任何边的 state 出现 | `[StateParent]` 特性 |

#### Analyzer 诊断

| 检测点 | 逻辑 | 报告位置 |
|--------|------|----------|
| DP036（字面量边未声明） | `StateTransitionLiteralEdgeAnalyzer` 在编译期扫描所有程序集的 `[StateMachine]` + `[Transition]` 特性，构建有效边集合，然后校验 `TryTransition` 调用的字面量 (state, trigger) 参数对 | `TryTransition` 调用参数 |

DP036 特性：
- 严重级别 **Info**（与 DP025 一致；运行时 `TryTransition` 仅返回 `false`，非错误）
- 跨程序集支持（表可在引用程序集中声明）
- 非常量参数跳过（运行时计算的值无法编译期校验）
- 仅校验 `TryTransition`（`CanTransitionFrom` 只接收 state，无 trigger 可校验）

```csharp
// DP036: (Cancelled, Submit) 未声明
table.TryTransition(OrderStatus.Cancelled, OrderTrigger.Submit, out _);
```

## 设计权衡

### enum-only 状态（简洁 vs 灵活）

选择 **enum-only** 状态/触发器，不支持 `string` / `int` 键。原因：

- 编译期生成冻结查找表需要编译期已知的键集合；enum 满足，string/int 不满足。
- enum 提供编译期类型安全与 IDE 补全；string/int 需运行时校验。
- 与 Stateless 等库差异化定位——Stateless 支持 string/enum，本库专注 enum 场景做到编译期极致。

代价：不支持任意类型状态。需要 string/int 键的场景使用 Stateless。

### 编译期展平（零运行时层级开销）

v3 层次状态选择 **编译期展平**（`HierarchyFlattener`）而非运行时层级遍历。原因：

- 运行时零层级开销——展平后的转换表与扁平表结构相同，`TryTransition` 查找路径无差异。
- 编译期已知所有 `[StateParent]` 关系，可在生成时展平。
- `IStateHierarchy<TState>` 仍提供运行时层级查询（`GetParent`、`IsInState`、`GetAncestors`），但转换路径不经过层级遍历。

代价：生成器复杂度增加（展平 + LCA + 复合委托合成）；层级关系变更需重新编译。

### LCA-based entry/exit 链

v3.3 选择 **LCA（最低公共祖先）算法**计算 entry/exit action 链。原因：

- LCA 确定从 `from` 到 `to` 转换时的最小 action 集合——不重复执行公共祖先的 action。
- Exit 链从 `from` 向上到 LCA（不含 LCA），Enter 链从 LCA 向下到 `to`（不含 LCA），符合 UML 状态机语义。
- 复合委托对运行时透明：`TransitionEdge` 结构不变。

### 部分执行语义

Entry/exit action 选择 **部分执行**语义——若 OnExit 成功后 OnEnter 抛异常，异常传播给调用方，OnExit 副作用已发生。原因：

- 补偿/回滚是业务逻辑，不在此库范围。
- `TransitionResult<TState>` 不暴露执行进度，避免增加复杂度。
- 调用方需自行处理补偿。

## 与生态的边界

### 与 Stateless 的关系

| | DesignPatterns State 转换表 | Stateless |
|---|---|---|
| 状态类型 | enum-only | enum / string / int |
| 转换表 | 编译期生成冻结查找表 | 运行时配置 |
| 诊断 | 编译期诊断（DP026–DP039, DP056–DP059） | 运行时异常 |
| 层次状态 | v3 编译期展平 | 运行时层级遍历 |
| 历史/并发状态 | 不支持 | 支持 |
| 持久化 | 不支持 | 支持（Stateless 本身不持久化，但可序列化配置） |
| 定位 | 轻量 primitive + 编译期胶水 | 通用状态机 DSL |

需要完整 UML 状态机（历史、并发、持久化）时使用 Stateless；需要编译期诊断与零运行时开销的 enum 状态机时使用本库。

### DI 集成

**当前已实现**：`DesignPatterns.Extensions.DependencyInjection`（MSDI）+ 生成器 `RegisterDi`。

- `RegisterDi` 使用 `TryAdd` 语义注册 `ITransitionTable<TState, TTrigger>`（v3 层次模式时额外注册 `IStateHierarchy<TState>`）。
- 手动注册扩展：`AddTransitionTable`（注册预构建表实例）、`AddStateMachine<TState, TTrigger>`、`AddStateHierarchy<TState, TTrigger>`（v3）。
- Core 不引用 DI；启用 DI 生成路径需引用扩展包（targets 打开 `DesignPatterns_EnableDiIntegration`）。

### Autofac

`DesignPatterns.Extensions.Autofac` — `RegisterAutofac`（见 [Autofac.md](../Autofac.md)）。

## 已知局限

- **不支持并发状态 / 历史状态**：v3 实现了层次状态，但并发 / 历史状态仍为非目标。
- **不支持 `string` / `int` 状态键**：仅支持 enum 状态/触发器。
- **不支持 guard 的编译期表达式求值**：v2 仅运行时委托 + 方法引用校验。
- **`StateMachine<TState, TTrigger>` 非线程安全**：设计为单线程使用。多线程场景应在调用方同步，或每线程使用独立实例。
- **部分执行语义**：OnExit 成功后 OnEnter 抛异常时，OnExit 副作用已发生，调用方需自行处理补偿。
- **Entry/Exit action 仅 async 路径触发**：同步 `TryTransition` 不调用 action。

## 参考

- [ADR-005: State transition table](../adr/ADR-005-state-transition-table.md)
- [AGENTS.md](../../AGENTS.md) — 项目规则与里程碑
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md) — 通用开发约定
- [Autofac.md](../Autofac.md) — Autofac 扩展
- [DesignPatterns.Samples.State](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.State) — 基础状态机（guard、entry/exit action、DI）
- [DesignPatterns.Samples.HierarchicalState](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.HierarchicalState) — 层次状态机（`[StateParent]`、边继承展平、action 链合成、`IStateHierarchy` DI）
