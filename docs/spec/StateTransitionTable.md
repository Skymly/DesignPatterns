# Spec: State 转换表

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/StateTransitionTable.md](../design/StateTransitionTable.md)
> **关联 ADR**：[ADR-005](../adr/ADR-005-state-transition-table.md)

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

## 诊断 ID

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

## 不变量

1. `TState` 和 `TTrigger` 均须为 **enum**（`where TState : struct, Enum`）。
2. Guard 方法签名须为 `bool Method(TState, TTrigger)`；须 **static**；须在 holder 类上声明。
3. Entry/Exit action 方法签名须为 `void Method(TState from, TState to, TTrigger trigger)`（sync）或 `ValueTask Method(TState from, TState to, TTrigger trigger, CancellationToken)`（async）；须 **static**；须在 holder 类上声明。
4. `StateMachine<TState, TTrigger>` **非线程安全**，设计为单线程使用。多线程场景应在调用方同步，或每线程使用独立实例。
5. 执行顺序（`TryTransitionAsync`）：guard → OnExit（sync → async）→ OnEnter（sync → async）→ 返回结果。
6. Entry/Exit action 仅通过 async 路径（`TryTransitionAsync`）触发；同步 `TryTransition` 不调用 action。
7. 部分执行语义：若 OnExit 成功后 OnEnter 抛异常，异常传播给调用方，OnExit 副作用已发生；`TransitionResult<TState>` 不暴露执行进度。
8. `TransitionTable<TState, TTrigger>` 不可变；`Build()` 后不可修改。
9. DI 注册使用 `TryAdd` 语义，重复注册不会覆盖。

## 兼容基线

- netstandard2.0 + net8.0（两者均须可用并随包分发）
- Roslyn 组件 4.8.0（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）
- 生成器实现：`IIncrementalGenerator` + `ForAttributeWithMetadataName`

## 不在范围内

- 并发状态（concurrent states）/ 历史状态（history states）
- `string` / `int` 状态键（仅支持 enum）
- Guard 的编译期表达式求值（v2 仅运行时委托 + 方法引用校验）
