# ADR-005: State transition table

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-14 |
| **关联 RFC** | [docs/rfc/archive/StateTransitionTable.md](../rfc/archive/StateTransitionTable.md)、[docs/rfc/archive/HierarchicalStateMachine.md](../rfc/archive/HierarchicalStateMachine.md) |

## 背景

DesignPatterns 缺少有限状态机 primitive。现有库（Stateless、Akka.NET）功能全面但厚重。需要一个轻量的、声明式标注 + 生成胶水 + 可选手动 Builder 的方案，与 Strategy/Factory 同级。

## 决策

采用 **enum 状态 + enum 触发器 + 编译期生成冻结查找表** 的方案：

- 运行时：`ITransitionTable<TState, TTrigger>` 接口 + `TransitionTableBuilder` 手动 Builder
- 编译期：`[StateMachine]` + `[Transition]` 特性 → 源生成器生成冻结查找表与强类型 API
- 诊断：DP026–DP039 覆盖重复边、非法 enum、guard 签名、entry/exit action、字面量边校验
- v3：层级状态机（`[StateParent]` + 编译期展平 + DP056–DP059）

**不是**通用状态机框架：层次状态（v3 之前）、历史、持久化、重试不在 v1 范围。

## 后果

**正面**：
- 与 Strategy/Factory/Chain 同级的轻量 primitive
- 编译期诊断拦截非法转换，运行时零分配查找
- 层级状态机（v3）通过编译期展平实现，运行时无层级开销

**负面**：
- 仅支持 enum 状态/触发器，不支持任意类型状态
- 层级展平增加了生成器复杂度

## 参考

- [docs/spec/StateTransitionTable.md](../spec/StateTransitionTable.md)
- [docs/design/StateTransitionTable.md](../design/StateTransitionTable.md)
