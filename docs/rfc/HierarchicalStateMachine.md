# RFC：State 层级状态机（编译期展平）

| 字段 | 值 |
|------|-----|
| **状态** | Draft（待评审） |
| **作者** | 维护者 |
| **创建** | 2026-06-28 |
| **关联** | [ROADMAP.md](../ROADMAP.md) F3 长期探索候选、[StateTransitionTable.md](../StateTransitionTable.md) v2、[rfc/StateTransitionTable.md](StateTransitionTable.md) §2.3 |
| **诊断** | `DP056` 起新区段 |

---

## 1. 摘要

在现有 State 转换表（v2：扁平 enum 图 + guard + entry/exit action + DI）基础上，增加 **层级状态（hierarchical states）** 支持：允许状态声明父状态，子状态自动继承父状态的转换边，父状态的 entry/exit action 在进入/离开子状态时按 UML 状态机语义链式触发。

**核心技术路线**：**编译期展平（compile-time flattening）**——源生成器在编译期将层级关系展开为扁平查找表，运行时 API 保持 `Dictionary<(TState, TTrigger), …>` 的 O(1) 查找性能，不引入运行时层级引擎。这与 ROADMAP 中「生成器展平为快表」的描述一致。

**不是**完整 UML 状态机框架：不做历史状态（history）、并发状态（orthogonal regions）、持久化快照。与 Stateless 的边界见 §9。

---

## 2. 动机

### 2.1 现有局限

当前 State 转换表（v2）是**扁平图**：每个 `(state, trigger)` 至多一条边，无父子关系。当领域模型存在自然层级时（见下例），扁平表需要为每个子状态重复声明相同的转换，导致冗余且易遗漏。

```text
订单状态层级：
  Active
  ├── Submitted
  └── Paid
  Cancelled
  Draft

问题：Submitted 和 Paid 都应响应 Cancel → Cancelled。
扁平表需要两条独立边：
  [Transition(Submitted, Cancel, Cancelled)]
  [Transition(Paid, Cancel, Cancelled)]
层级表只需一条父级边：
  [Transition(Active, Cancel, Cancelled)]  ← 子状态自动继承
```

### 2.2 为何现在

- F1/F2/F2+ 全部完成，0.2.0-preview3 已发布，功能主线收敛。
- State v2（guard + entry/exit + DI + tracing）已稳定，层级状态是 v1 RFC §2.3 明确列出的「暂不做」项中**探索价值最高**的（⭐⭐⭐）。
- 生成器管线已模块化（Transform → Validate → Emit），扩展层级元数据收集与展平逻辑的侵入性低。
- 与 Stateless 重叠但展示「编译期展平层级」技术——这正是本库「编译期 + 运行时协同」探索方针的核心体现。

### 2.3 非目标

- **历史状态**（shallow/deep history）——UML 超集，运行时需要记住最后活跃子状态，与「编译期展平」路线冲突。
- **并发状态**（orthogonal regions / parallel states）——需要多活跃状态，当前 `IStateMachine.CurrentState` 是单值。
- **运行时动态层级修改**——层级在编译期固定；运行时不可增删父子关系。
- **持久化快照 / 分布式状态 / 超时转换**——与 v1 非目标一致。
- **`string` / `int` 状态键**——仍仅支持 `enum`。

---

## 3. 设计原则

1. **编译期展平，运行时扁平**：生成器在编译期将层级关系展开为扁平边表；运行时查找逻辑与 v2 完全一致（O(1) Dictionary lookup），不引入层级遍历开销。
2. **子优先继承**：子状态的直接边覆盖父状态的继承边（most-specific wins）。
3. **Action 链由生成器合成**：entry/exit action 链在编译期合成为复合委托，运行时 `TransitionEdge` 结构不变（仍为单个 `OnEnter` / `OnExit` 委托，但委托体内部按序调用链）。
4. **向后兼容**：未声明层级的现有状态机行为不变；层级是 opt-in。
5. **层级元数据可选暴露**：生成器额外输出 `IStateHierarchy<TState>` 实现，供 `IsInState` / `GetParent` 查询；不影响转换查找路径。

---

## 4. 概念模型

```text
       Cancel trigger
  [Active] ─────────────────► [Cancelled]
    │
    ├── [Submitted]  ──Pay──► [Paid]
    │        │
    │     Submit
    │        │
    │        ▼
    │  (self-loop or edge)
    │
    └── [Paid]

  [Draft] ──Submit──► [Submitted]
```

- **父状态（parent state）**：一个 enum 成员，声明为其他状态的父。父状态本身可以是叶子（有出边）也可以是纯容器（无自身边，仅提供继承边）。
- **子状态（child state）**：声明了 `Parent` 的状态。子状态继承父状态的所有转换边（除非自身有同 trigger 的直接边）。
- **祖先链（ancestor chain）**：从当前状态向上到根的路径：`Submitted → Active → (root)`。
- **LCA（最低公共祖先）**：转换 `from → to` 时，from 和 to 的最近公共祖先。exit action 从 from 向上执行到 LCA（不含 LCA）；entry action 从 LCA 向下执行到 to（不含 LCA）。

### 4.1 转换解析规则

当 `TryTransition(current, trigger)` 被调用时：

1. 查找 `(current, trigger)` 的直接边 → 命中则使用
2. 若未命中，查找 `(parent(current), trigger)` → 命中则使用
3. 继续向上直到根 → 命中则使用
4. 全部未命中 → 返回 `false`

**编译期展平**：生成器在编译期执行上述解析，为每个 `(state, trigger)` 对预计算有效边，写入扁平表。运行时只需一次 Dictionary 查找。

### 4.2 Entry/Exit Action 链

转换 `Submitted → Cancelled`（LCA = root，即无公共祖先）：

```text
1. Exit Submitted  (子 exit action)
2. Exit Active     (父 exit action)
3. Enter Cancelled (entry action)
```

转换 `Submitted → Paid`（LCA = Active，父未变）：

```text
1. Exit Submitted  (子 exit action)
2. Enter Paid      (子 entry action)
（Active 的 exit/enter 不触发——父状态未变）
```

**编译期合成**：生成器为每条展平后的边计算 action 链，生成复合委托：

```csharp
// 生成代码（示意）
static void ExitChain_Submitted_To_Cancelled(TState from, TState to, TTrigger trigger)
{
    OnExitSubmitted(from, to, trigger);  // 子 exit
    OnExitActive(from, to, trigger);     // 父 exit
}
static void EnterChain_Submitted_To_Cancelled(TState from, TState to, TTrigger trigger)
{
    OnEnterCancelled(from, to, trigger);
}
```

---

## 5. 运行时 API（草案）

### 5.1 层级元数据接口（新增）

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// Optional hierarchy metadata for hierarchical state machines.
/// Generated tables implement this interface when Hierarchical = true.
/// </summary>
public interface IStateHierarchy<in TState>
    where TState : struct, Enum
{
    /// <summary>
    /// Returns the parent state of <paramref name="state"/>, or null if it is a root.
    /// </summary>
    TState? GetParent(TState state);

    /// <summary>
    /// Returns true if <paramref name="current"/> is <paramref name="ancestor"/>
    /// or a descendant of <paramref name="ancestor"/>.
    /// </summary>
    bool IsInState(TState current, TState ancestor);

    /// <summary>
    /// Returns the ancestor chain from <paramref name="state"/> up to root (exclusive of root).
    /// </summary>
    IReadOnlyList<TState> GetAncestors(TState state);
}
```

### 5.2 ITransitionTable 扩展

`ITransitionTable<TState, TTrigger>` **不**新增成员。层级表的生成实现额外实现 `IStateHierarchy<TState>`：

```csharp
// 生成产物
public sealed partial class OrderStatusTransitionTable
    : ITransitionTable<OrderStatus, OrderTrigger>
    , IStateHierarchy<OrderStatus>  // 仅 Hierarchical = true 时
{ ... }
```

消费者可通过模式匹配访问层级元数据：

```csharp
if (table is IStateHierarchy<OrderStatus> hierarchy)
{
    bool isActive = hierarchy.IsInState(current, OrderStatus.Active);
}
```

### 5.3 扩展方法

```csharp
namespace DesignPatterns.Behavioral;

public static class StateHierarchyExtensions
{
    /// <summary>
    /// Attempts to cast the table to IStateHierarchy and query IsInState.
    /// Returns false if the table is not hierarchical.
    /// </summary>
    public static bool IsInState<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table,
        TState current, TState ancestor)
        where TState : struct, Enum
        where TTrigger : struct, Enum
        => table is IStateHierarchy<TState> h && h.IsInState(current, ancestor);
}
```

### 5.4 手动 Builder 扩展

```csharp
public sealed class TransitionTableBuilder<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    // 现有 API 不变

    /// <summary>Declares a parent-child relationship. Opt-in for hierarchical mode.</summary>
    public TransitionTableBuilder<TState, TTrigger> WithParent(
        TState child, TState parent);
}
```

`Build()` 返回的 `ITransitionTable` 实现在 `WithParent` 被调用后额外实现 `IStateHierarchy<TState>`。手动 Builder 不做展平（用户需自行添加所有有效边）；层级元数据仅用于 `IsInState` 查询。

---

## 6. 编译期 API（草案）

### 6.1 新增特性

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// Declares a parent state for a child state in a hierarchical state machine.
/// Apply on the holder class, once per child state that has a parent.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class StateParentAttribute : Attribute
{
    public StateParentAttribute(object child, object parent) { ... }
}
```

`[StateMachine]` 新增属性：

```csharp
public sealed class StateMachineAttribute : Attribute
{
    // 现有成员不变

    /// <summary>
    /// Enables hierarchical state machine mode.
    /// When true, [StateParent] attributes are collected and transitions are flattened.
    /// Default: false (backward compatible).
    /// </summary>
    public bool Hierarchical { get; set; }
}
```

### 6.2 用法示例

```csharp
public enum OrderStatus
{
    Draft,
    Active,      // composite/parent state
    Submitted,
    Paid,
    Cancelled,
}

public enum OrderTrigger
{
    Submit,
    Pay,
    Cancel,
}

[StateMachine(
    typeof(OrderStatus),
    typeof(OrderTrigger),
    Initial = OrderStatus.Draft,
    Hierarchical = true)]
[StateParent(OrderStatus.Submitted, OrderStatus.Active)]
[StateParent(OrderStatus.Paid, OrderStatus.Active)]

// 子状态直接边
[Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
[Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]

// 父级继承边：Submitted 和 Paid 都继承此边
[Transition(OrderStatus.Active, OrderTrigger.Cancel, OrderStatus.Cancelled)]

// 父级 entry/exit action（子状态进入/离开时链式触发）
[Transition(OrderStatus.Active, OrderTrigger.Cancel, OrderStatus.Cancelled,
    OnExit = nameof(OnExitActive))]
public static partial class OrderStatusMachine;

static partial void OnExitActive(OrderStatus from, OrderStatus to, OrderTrigger trigger)
    => Console.WriteLine($"Exiting Active (was in {from})");
```

### 6.3 生成产物

| 生成类型 | 说明 |
|----------|------|
| `OrderStatusTransitionTable` | 扁平表（展平后）+ `IStateHierarchy<OrderStatus>` 实现 |
| `OrderStatusMachine`（partial 扩展） | 便捷方法（与 v2 一致） |
| `OrderStatusStateMachine` | `IStateMachine<OrderStatus, OrderTrigger>` 包装器 |

展平后的表等效于：

```text
(Draft, Submit)       → Submitted
(Submitted, Pay)      → Paid
(Submitted, Cancel)   → Cancelled  ← 从 Active 继承
(Paid, Cancel)        → Cancelled  ← 从 Active 继承
```

### 6.4 生成器实现要点

在现有 `StateTransitionGenerator` 管线上扩展：

1. **Transform 阶段**：
   - 当 `Hierarchical = true` 时，额外收集 `[StateParent]` 特性
   - 构建 `Dictionary<TState, TState>` parent map
   - 检测循环（DP056）

2. **Validate 阶段**（新增诊断）：
   - DP056：层级循环（A → B → A）
   - DP057：`[StateParent]` 的 child 或 parent 不是声明的状态 enum 成员
   - DP058：`[StateParent]` 的 parent 等于 child（自引用）
   - DP059：父状态从未作为 `from` 出现且无子状态（Info，孤立父状态）

3. **Flatten 阶段**（新增）：
   - 对每个 `(state, trigger)` 对，沿祖先链向上查找有效边
   - 子状态直接边优先（覆盖父级边）
   - 为每条展平边计算 entry/exit action 链（LCA 算法）
   - 生成复合委托包装 action 链

4. **Emit 阶段**：
   - 输出扁平表（与 v2 格式一致，但边数可能更多）
   - 额外输出 `IStateHierarchy<TState>` 实现（`GetParent` / `IsInState` / `GetAncestors`）
   - 生成 `StateParentMap` 静态字段供层级查询

---

## 7. 诊断 ID（DP056–DP059）

下一可用 ID：**DP056**。

| ID | 级别 | 归属 | 触发条件 |
|----|------|------|----------|
| **DP056** | Error | 生成器 | 层级循环：状态 A 的父链经 B、C…回到 A |
| **DP057** | Error | 生成器 | `[StateParent]` 的 child 或 parent 不是 `[StateMachine]` 声明的状态 enum 成员 |
| **DP058** | Error | 生成器 | `[StateParent]` 的 parent 等于 child（自引用） |
| **DP059** | Info | 生成器 | 父状态从未作为 `from` 出现且无子状态声明它为父（孤立父状态） |

**CodeFix**：暂不提供（层级关系需人工判断）。

---

## 8. Entry/Exit Action 链算法

### 8.1 LCA 计算

```text
function LCA(a, b):
    ancestors_a = [a] + GetAncestors(a)
    ancestors_b = [b] + GetAncestors(b)
    return first common element in ancestors_a ∩ ancestors_b
    (or null if no common ancestor = root)
```

### 8.2 Exit 链（from → LCA，不含 LCA）

```text
function ExitChain(from, lca):
    chain = []
    current = from
    while current != lca and current != null:
        if HasOnExit(current): chain.append(GetOnExit(current))
        current = GetParent(current)
    return chain  // 执行顺序：from → ... → child-of-lca
```

### 8.3 Enter 链（LCA → to，不含 LCA，逆序执行）

```text
function EnterChain(to, lca):
    chain = []
    current = to
    while current != lca and current != null:
        if HasOnEnter(current): chain.prepend(GetOnEnter(current))
        current = GetParent(current)
    return chain  // 执行顺序：child-of-lca → ... → to
```

### 8.4 同级转换（LCA = from 或 LCA = to）

- `from == to`（自环）：exit(from) → enter(from)
- `LCA == from`（进入后代）：仅 enter 链（from 的 exit 不触发）
- `LCA == to`（从后代回到祖先）：仅 exit 链（to 的 enter 不触发）

---

## 9. 与 Stateless 的边界

| | 本库层级状态机 | [Stateless](https://github.com/dotnet-state-machine/stateless) |
|--|-------------------|----------------------------------------------------------------|
| 层级模型 | 编译期声明 + 展平 | 运行时 fluent API（`SubstateOf`） |
| 查找性能 | O(1) 扁平表（展平后） | O(depth) 运行时向上查找 |
| 层级修改 | 编译期固定 | 运行时可配置 |
| 历史状态 | 不支持 | 支持（`OnEntry` from history） |
| 并发状态 | 不支持 | 支持（`DefineParallelState`） |
| Action 链 | 编译期合成委托 | 运行时动态调用 |
| 诊断 | 编译期 DP056–DP059 | 无编译期诊断 |
| 适用 | 层级固定、追求零运行时开销 | 需要运行时灵活性 / 历史状态 / 并发 |

**指导**：层级关系在编译期已知且不需运行时修改 → 本库；需要 `SubstateOf` 动态配置 / 历史状态 / 并发区域 → Stateless。

---

## 10. 交付计划

| 阶段 | 内容 | 依赖 |
|------|------|------|
| **v3.1** | 运行时 `IStateHierarchy<TState>` + `TransitionTableBuilder.WithParent` + `IsInState` 扩展方法 | 无 |
| **v3.2** | 生成器：`[StateParent]` 收集 + 展平算法 + DP056–DP059 诊断 | v3.1 |
| **v3.3** | 生成器：entry/exit action 链合成 + LCA 算法 | v3.2 |
| **v3.4** | DI 集成（`IStateHierarchy` 注册与解析） + Sample + Docs | v3.3 |

每个阶段独立 PR，遵守单模块 PR 边界。

---

## 11. 开放问题

### Q1：父状态能否也是活跃状态？

**问题**：`Active` 既是 `Submitted`/`Paid` 的父，能否也是 `TryTransition` 的 `from`（即直接处于 `Active` 状态而非其子状态）？

**倾向**：允许。父状态本身可以是叶子状态。`IsInState(Active, Active)` 返回 `true`。展平时 `(Active, trigger)` 的直接边与子状态继承的边独立存在。

### Q2：多重继承？

**问题**：一个子状态能否有多个父状态？

**倾向**：不支持。单继承（每个状态至多一个 parent）。多重继承引入 LCA 歧义和复杂度，不符合「primitive」定位。需要多重继承的场景用 Stateless。

### Q3：通配转换（wildcard transition）？

**问题**：ROADMAP 提及「通配转换」。是否需要一个特殊的「任意状态」枚举值或特性来声明全局 fallback 边？

**倾向**：v3 不做。父级继承边已覆盖大部分「通配」语义（在父状态上声明边即对所有子状态生效）。如果后续有需求，可评估 `[Transition(Any, trigger, to)]` 语法。

### Q4：`GetAllowedTriggers` 是否包含继承的触发器？

**问题**：当 `Submitted` 没有直接 `Cancel` 边但 `Active` 有时，`GetAllowedTriggers(Submitted)` 是否返回 `Cancel`？

**倾向**：是。展平后 `Submitted` 的有效边包含继承的 `Cancel`，`GetAllowedTriggers` 返回所有有效触发器。

### Q5：`TransitionTrace` 如何表示 action 链？

**问题**：现有 `TransitionTrace` 有 `OnExitCompleted` / `OnEnterCompleted` 两个 bool。层级 action 链可能有多个 exit/enter action。

**倾向**：扩展 `TransitionTrace<TState>` 增加 `IReadOnlyList<string> ExitActionsExecuted` / `EnterActionsExecuted`（方法名列表）。保持 `OnExitCompleted` / `OnEnterCompleted` 作为「全部成功」的聚合 bool。

---

## 12. 向后兼容性

| 现有 API | 影响 |
|----------|------|
| `ITransitionTable<TState, TTrigger>` | **不变**（无新成员） |
| `TransitionTableBuilder<TState, TTrigger>` | 新增 `WithParent` 方法（additive） |
| `IStateMachine<TState, TTrigger>` | **不变** |
| `[StateMachine]` | 新增 `Hierarchical` 属性（默认 `false`） |
| `[Transition]` | **不变** |
| 现有扁平状态机 | **零影响**（`Hierarchical` 默认 `false`，生成器行为不变） |
| `TransitionTrace<TState>` | 可能扩展（Q5），additive |
| `TransitionEdge<TState, TTrigger>` | **不变**（action 链由生成器合成为单个委托） |

**破坏性变更**：无。层级模式完全 opt-in。
