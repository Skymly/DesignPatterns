# Design Doc 索引

设计文档（Design Document）— 记录模式的实现细节、设计权衡、已知局限。

- **格式与变更门槛**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#5-design-doc--设计文档)
- **模板**：[_template.md](_template.md)
- **与 Spec 的关系**：[Spec](../spec/) 描述 **what**（契约），Design Doc 描述 **how** + **why**（实现）

## 已有 Design Doc

| 模式 | Design Doc | Spec | 关联 ADR |
|------|------------|------|----------|
| Strategy | [Strategy.md](Strategy.md) | [Strategy.md](../spec/Strategy.md) | — |
| Chain of Responsibility | [ChainOfResponsibility.md](ChainOfResponsibility.md) | [ChainOfResponsibility.md](../spec/ChainOfResponsibility.md) | — |
| Composite | [Composite.md](Composite.md) | [Composite.md](../spec/Composite.md) | [ADR-006](../adr/ADR-006-composite-parallel-traversal.md)、[ADR-007](../adr/ADR-007-composite-tree-schema-validation.md) |
| Factory Registry | [FactoryRegistry.md](FactoryRegistry.md) | [FactoryRegistry.md](../spec/FactoryRegistry.md) | — |
| Decorator | [Decorator.md](Decorator.md) | [Decorator.md](../spec/Decorator.md) | — |
| Event Aggregator | [EventAggregator.md](EventAggregator.md) | [EventAggregator.md](../spec/EventAggregator.md) | — |
| State Transition Table | [StateTransitionTable.md](StateTransitionTable.md) | [StateTransitionTable.md](../spec/StateTransitionTable.md) | [ADR-005](../adr/ADR-005-state-transition-table.md) |

## 迁移状态

所有 7 个模式文档已从 `docs/<PatternName>.md` 拆分迁移至 `docs/spec/` + `docs/design/`。旧文件已删除。
