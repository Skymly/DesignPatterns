# RFC：State 转换表（有限状态转换 primitive）

| 字段 | 值 |
|------|-----|
| **状态** | Draft（**v1 设计决策已确认**，待 M1 实现） |
| **作者** | 维护者 / 贡献者 |
| **创建** | 2026-06-14 |
| **关联** | [ROADMAP.md](../ROADMAP.md) F3、`DP026` 起新诊断区段 |
| **评审后** | 通过 → 迁入 `docs/StateTransitionTable.md`；实现拆 Issue → 按 [AGENTS.md](../../AGENTS.md) 模块边界 PR |

---

## 1. 摘要

为 DesignPatterns 增加 **有限状态转换表（finite state transition table）** primitive：用 **enum 状态 + enum 触发器** 描述合法边，编译期生成冻结查找表与强类型 API，编译期诊断拦截重复边、非法枚举成员与明显结构问题。

**不是**通用状态机框架（层次状态、历史、持久化、重试、entry/exit 动作 DSL）。定位与 Strategy/Factory 同级：**声明式标注 + 生成胶水 + 可选手动 Builder**。

---

## 2. 动机

### 2.1 库内缺口

| 已有能力 | 表达力 |
|----------|--------|
| Strategy / Factory | key → 实现（无「当前态」） |
| Chain / Decorator | 线性顺序（非分支图） |
| Composite | 树结构（非状态图） |
| EventAggregator | 事件广播（无状态合法性） |

许多领域问题本质是 **(当前状态, 触发器) → 下一状态** 的有限图：订单生命周期、插件启停、连接状态、向导步骤、任务阶段。手写 `switch` 或 `Dictionary<(State, Trigger), State>` 易漏边、难测试、重构 enum 时无编译期保护。

### 2.2 为何现在

- F1/F2 主线已收敛，F3 准入评审通过本 RFC 后可排期。
- 与现有生成器模式（partial holder、`ForAttributeWithMetadataName`、`*Keys` 常量风格）一致，可复用注册表类基础设施思路。
- 不重复 MediatR（命令路由）或 Polly（弹性）；与 **Stateless** 等库边界清晰（见 §8）。

### 2.3 非目标（v1 明确不做）

- 层次 / 并发 / 历史状态（UML 状态机超集）
- 持久化快照、分布式状态、超时转换
- Entry / exit / internal transition 动作（consumer 在 `TryTransition` 成功后自行副作用）
- Guard 的编译期表达式求值（v1 无 guard；v2 仅运行时委托）
- `string` / `int` 状态键（v1 仅 **enum**；与 DP025 字面量键校验正交，后续再评）
- 反射扫描或 AppDomain 自动发现转换

---

## 3. 设计原则

1. **表驱动，非继承**：状态行为留在 consumer 类型中；库只提供 **合法性表 + TryTransition**。
2. **编译期优先**：非法边、重复边、引用未知 enum 成员 → **Error**；孤立状态 → **Info**（可配置关闭）。
3. **组合优于继承**：不引入 `StateBase` / `AbstractState` 体系。
4. **与生态共存**：表可嵌入领域服务；需要完整状态机 DSL 时继续用 Stateless 等，本库不竞争。
5. **分阶段交付**：v1 纯表；v2 guard + DI；v3 再评 string trigger / 与 EventAggregator 联动示例。

---

## 4. 概念模型

```text
         trigger T1                trigger T2
  [S0] ──────────────► [S1] ──────────────► [S2]
         (唯一边)              (唯一边)
```

- **状态 `TState`**：`enum`（consumer 定义）
- **触发器 `TTrigger`**：**独立** `enum`（consumer 定义；v1 不与 `TState` 共用同一 enum）
- **边**：至多一条 `(TState from, TTrigger trigger) → TState to`；**允许自环**（`from == to`）
- **初始态**：机器元数据指定一个 `TState` 常量
- **终态**：v1 **不**建模终态 attribute 或专用规则；无出边的状态自然表示终态

---

## 5. 运行时 API（草案）

命名空间：`DesignPatterns.Behavioral`（与 Strategy、Chain、EventAggregator 同级）。

### 5.1 核心接口

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// Immutable transition table for enum state and trigger types.
/// </summary>
public interface ITransitionTable<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
  TState InitialState { get; }

  /// <summary>
  /// Returns false when (current, trigger) is not a declared transition.
  /// </summary>
  bool TryTransition(TState current, TTrigger trigger, out TState next);

  /// <summary>
  /// Triggers that have at least one outgoing edge from <paramref name="current"/>.
  /// Order is declaration order in source.
  /// </summary>
  IReadOnlyList<TTrigger> GetAllowedTriggers(TState current);

  /// <summary>
  /// Whether any edge leaves <paramref name="current"/>.
  /// </summary>
  bool CanTransitionFrom(TState current);
}
```

### 5.2 异常类型（可选）

```csharp
public sealed class InvalidTransitionException<TState, TTrigger> : Exception
    where TState : struct, Enum
    where TTrigger : struct, Enum;
```

提供扩展方法 `Transition(...)` 在失败时抛上述异常；与 `StrategyRegistry.Get` 对称。

### 5.3 手动 Builder（无生成器时）

```csharp
public sealed class TransitionTableBuilder<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
  public TransitionTableBuilder<TState, TTrigger> WithInitial(TState initial);
  public TransitionTableBuilder<TState, TTrigger> Add(TState from, TTrigger trigger, TState to);
  public ITransitionTable<TState, TTrigger> Build();
}
```

- 重复 `(from, trigger)` → `ArgumentException`（与 Factory Builder 一致）
- net8.0 实现内部可用 `FrozenDictionary<(TState, TTrigger), TState>`；netstandard2.0 用 `Dictionary`

### 5.4 与 EventAggregator / Strategy 的组合（consumer 侧）

```csharp
if (table.TryTransition(order.Status, OrderTrigger.Pay, out var next))
{
  order.Status = next;
  await aggregator.PublishAsync(new OrderPaidEvent(order.Id), ct);
}
```

库 **不** 内建发布事件或调用 Strategy；RFC 仅在 Sample 演示组合。

---

## 6. 编译期 API（草案）

### 6.1 特性

```csharp
namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a partial static holder for generated transition table metadata.
/// Apply exactly once per state machine.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StateMachineAttribute : Attribute
{
  public StateMachineAttribute(Type stateType, Type triggerType) { ... }

  /// <summary>Initial state enum member.</summary>
  public object Initial { get; set; } = null!;

  /// <summary>Optional machine name for generated type names; default = state enum name sans leading 'E'.</summary>
  public string? Name { get; set; }
}

/// <summary>
/// Declares one directed edge. Repeat on the same partial holder class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TransitionAttribute : Attribute
{
  public TransitionAttribute(object from, object trigger, object to) { ... }
}
```

**约束（生成器 Error）**

- Holder 必须为 `static partial class`（**一 holder 一机**；v1 不支持同一 holder 多台状态机）
- `StateMachineAttribute.StateType` / `TriggerType` 须为 **两个独立** enum
- `TransitionAttribute` 的 `from` / `trigger` / `to` 须为该 enum 的**命名常量**（`OrderStatus.Draft`），非任意 int cast
- 同一 holder 上 `(from, trigger)` 唯一
- `from == to` 的自环边合法

### 6.2 用法示例

```csharp
public enum OrderStatus { Draft, Submitted, Paid, Cancelled }

public enum OrderTrigger { Submit, Pay, Cancel }

[StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
public static partial class OrderStatusMachine
{
  [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
  [Transition(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)]
  [Transition(OrderStatus.Draft, OrderTrigger.Cancel, OrderStatus.Cancelled)]
  [Transition(OrderStatus.Submitted, OrderTrigger.Cancel, OrderStatus.Cancelled)]
}
```

### 6.3 生成产物命名

对 `OrderStatus` + holder `OrderStatusMachine`：

| 生成类型 | 说明 |
|----------|------|
| `OrderStatusTransitionTable` | `ITransitionTable<OrderStatus, OrderTrigger>` 实现 + `static Instance` |
| `OrderStatusMachine`（同名 partial 扩展） | 便捷：`public static bool TryTransition(...)` 转发到 `TransitionTable.Instance` |

v1 **不**生成 `{State}Triggers` 或 `{State}Keys` 常量类——触发器与状态均直接使用 consumer 定义的 enum 成员。

命名规则写入 [ROADMAP.md](../ROADMAP.md)「命名规则」表：

| 模式 | 生成类型示例 |
|------|----------------|
| State | `{StateEnum}TransitionTable`、partial `{Holder}` 便捷方法 |

### 6.4 生成器实现要点

- 新增 `StateTransitionGenerator`：`IIncrementalGenerator` + `ForAttributeWithMetadataName`
- 收集同一 partial holder 上所有 `TransitionAttribute`（需 syntax 或 semantic 聚合；参考 `HandlerOrder` 多特性模式）
- 输出单文件 partial：`TransitionTable` + 扩展方法
- 注册 `[Generator]` / `AnalyzerReleases.Unshipped.md`

---

## 7. 诊断 ID（预留 DP026–DP032）

下一可用 ID：**DP026**（[AGENTS.md](../../AGENTS.md)）。实现前在 `DiagnosticIds.cs` 正式登记。

| ID | 级别 | 归属 | 触发条件 |
|----|------|------|----------|
| **DP026** | Error | 生成器 | 重复 `(from, trigger)` |
| **DP027** | Error | 生成器 | `Transition` 中 `from`/`to` 不是 `StateMachine` 声明的状态 enum 成员 |
| **DP028** | Error | 生成器 | `trigger` 不是声明的触发器 enum 成员 |
| **DP029** | Error | 生成器 | `Initial` 不是状态 enum 成员 |
| **DP030** | Error | 生成器 | `[StateMachine]` holder 不是 `static partial class` |
| **DP031** | Info | 生成器 | 状态 enum 成员从未作为 `from` 出现（**孤立态**，排除 `Initial` 仅作终态场景） |
| **DP032** | Info | Analyzer（v2） | 调用了 `Transition(...)` 抛异常路径且字面量 `(state, trigger)` 不在表内（与 DP025 对称，**v2**） |

**CodeFix（v1）**

- DP026：无自动修复（删重复 attribute）
- DP027–DP029：若 typo 接近某 enum 成员，可选「替换为最近成员」CodeFix（复用 CorrectRegistryKey 距离算法）

**明确不做 CodeFix**

- DP031 孤立态（需业务判断是否故意终态）

---

## 8. 与 Stateless / 手写表 的边界

| | 本库 State 转换表 | [Stateless](https://github.com/dotnet-state-machine/stateless) |
|--|-------------------|----------------------------------------------------------------|
| 模型 | 扁平 enum 图 | 状态机 + guard + 行为 + 子状态 |
| 编译期 | 边表 + DP | 主要为运行时 fluent API |
| 体积 | 单表 + TryTransition | 功能完整 |
| 适用 | 合法边校验、轻量 domain | 复杂工作流、回调、触发器参数 |

**指导**：边数 &lt; 30、无 guard 的 domain → 本库；需要 `OnEntry` / `PermitReentry` / 子状态 → Stateless。

---

## 9. 示例场景（Samples 规划）

| Sample 项目 | 演示 |
|-------------|------|
| `DesignPatterns.Samples.StateMachine` | 订单状态 + `TryTransition` + 非法触发单元测试 |
| （可选）与 EventAggregator | 转换成功后 `PublishAsync`（Sample 内组合，非库 API） |
| （可选）手动 Builder | 与生成表等价的手写 `TransitionTableBuilder` 测试 |

---

## 10. DI 集成（v2，本 RFC 仅登记）

```csharp
// 草案，实现于 Extensions.DependencyInjection + 生成器 targets
OrderStatusTransitionTable.RegisterDi(services); // Singleton ITransitionTable<,> 或具体表类型
```

v1 **不** 阻塞：表为无状态单例，`new` 或 `Instance` 即可。

---

## 11. 实现分期

| 阶段 | 范围 | 交付 |
|------|------|------|
| **M0 评审** | 本 RFC 定稿 | Issue + 标签 `rfc-approved` |
| **M1 Runtime** | `ITransitionTable`、`TransitionTableBuilder`、异常、单元测试 | PR → Runtime 模块 |
| **M2 Generator** | `[StateMachine]`、`[Transition]`、`{State}TransitionTable`、DP026–DP031 | PR → SourceGenerators + Diagnostics |
| **M3 Sample + Docs** | Samples 项目、DesignPatterns.Docs 用户页、`docs/StateTransitionTable.md` | 分 PR |
| **M4（可选）v2** | Guard 委托、DP032、RegisterDi | 单独 RFC 修订 |

**模块边界**（强制）：M1 / M2 / M3 各为独立 PR，符合 [AGENTS.md](../../AGENTS.md) 跨模块规则。

---

## 12. 测试策略

| 层级 | 内容 |
|------|------|
| 单元 | `TryTransition` 命中/未命中、`GetAllowedTriggers` 顺序、Builder 重复边 |
| 生成器 Verify | 表源码快照、DP026–DP030 消息与位置 |
| 集成 | 生成 `TransitionTable` → 运行时行为与手写 Builder 一致 |
| Analyzer（v2） | DP032 字面量边校验 |

---

## 13. v1 设计决策（已确认）

| # | 决策 | 结论 |
|---|------|------|
| Q1 | 触发器是否必须独立 enum？ | **是** — `TState` 与 `TTrigger` 必须为两个独立 enum |
| Q2 | 是否生成 `{State}Triggers` const 类？ | **否** — v1 不生成；直接使用 consumer 的 trigger enum |
| Q3 | DP031 孤立态默认级别 | **Info** |
| Q4 | `Transition` 是否允许 `from == to`（自环）？ | **允许** |
| Q5 | 终态是否要在表中有出边 / 是否建模终态？ | **不强制、不建模** — 无出边即自然终态 |
| Q6 | 一个 holder 是否只允许一台状态机？ | **是** — 一 holder 一机 |

后续若需 string trigger 或 trigger 常量类，单独开 v2 RFC 修订，不在 v1 范围。

---

## 14. 评审检查清单

- [x] 与「不做全量 GoF / 不做 MediatR」无冲突
- [x] 命名与现有 `{Contract}Keys` / `{Contract}Registry` 可区分（v1 不生成 Keys/Triggers 常量）
- [x] DP026–DP031 区间无与既有 ID 重叠
- [x] netstandard2.0 + net8.0 双 TFM 表实现可行
- [x] v1 设计决策（§13 Q1–Q6）已确认
- [ ] 至少一个 Skymly 产品场景愿意在 preview 包上试用（AnyTool 插件生命周期 / AgentDesk 任务态等）
- [ ] M1→M2→M3 分期与独立 PR 启动

---

## 15. 参考

- [Strategy.md](../Strategy.md) — 注册表 + 生成器模式参考
- [FactoryKeyConventions.md](../FactoryKeyConventions.md) — 键约定（State v1 不用 string key）
- [EventAggregator.md](../EventAggregator.md) — 组合用 pub/sub
- [ROADMAP.md](../ROADMAP.md) F3 准入标准
- Stateless 文档 — 生态边界对照

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-06-14 | 初稿（Draft） |
| 2026-06-15 | v1 设计决策确认（§13 Q1–Q6） |
