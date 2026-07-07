# Spec 索引

规范文档（Specification）— 定义模式的稳定契约：API 面、诊断 ID、不变量、兼容基线。

- **格式与变更门槛**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#4-spec--规范文档)
- **模板**：[_template.md](_template.md)
- **与 Design Doc 的关系**：Spec 描述 **what**（契约），[Design Doc](../design/) 描述 **how** + **why**（实现）

## 已有 Spec

| 模式 | Spec | Design Doc | 关联 ADR |
|------|------|------------|----------|
| Strategy | [Strategy.md](Strategy.md) | [Strategy.md](../design/Strategy.md) | — |
| Chain of Responsibility | [ChainOfResponsibility.md](ChainOfResponsibility.md) | [ChainOfResponsibility.md](../design/ChainOfResponsibility.md) | — |
| Composite | [Composite.md](Composite.md) | [Composite.md](../design/Composite.md) | [ADR-006](../adr/ADR-006-composite-parallel-traversal.md)、[ADR-007](../adr/ADR-007-composite-tree-schema-validation.md) |
| Factory Registry | [FactoryRegistry.md](FactoryRegistry.md) | [FactoryRegistry.md](../design/FactoryRegistry.md) | — |
| Decorator | [Decorator.md](Decorator.md) | [Decorator.md](../design/Decorator.md) | — |
| Event Aggregator | [EventAggregator.md](EventAggregator.md) | [EventAggregator.md](../design/EventAggregator.md) | — |
| State Transition Table | [StateTransitionTable.md](StateTransitionTable.md) | [StateTransitionTable.md](../design/StateTransitionTable.md) | [ADR-005](../adr/ADR-005-state-transition-table.md) |

## 迁移状态

所有 7 个模式文档已从 `docs/<PatternName>.md` 拆分迁移至 `docs/spec/` + `docs/design/`。旧文件已删除。
