# Spec 索引

规范文档（Specification）— 定义模式的稳定契约：API 面、诊断 ID、不变量、兼容基线。

- **格式与变更门槛**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#4-spec--规范文档)
- **模板**：[_template.md](_template.md)
- **与 Design Doc 的关系**：Spec 描述 **what**（契约），[Design Doc](../design/) 描述 **how** + **why**（实现）

## 已有 Spec

（Spec 文档将随模式文档迁移逐步创建。现有模式文档位于 `docs/<PatternName>.md`，将拆分迁移至 `docs/spec/` + `docs/design/`。）

## 迁移计划

| 模式 | 现有文档 | Spec 目标 | Design Doc 目标 |
|------|----------|-----------|-----------------|
| Strategy | [docs/Strategy.md](../Strategy.md) | `docs/spec/Strategy.md` | `docs/design/Strategy.md` |
| Chain of Responsibility | [docs/ChainOfResponsibility.md](../ChainOfResponsibility.md) | `docs/spec/ChainOfResponsibility.md` | `docs/design/ChainOfResponsibility.md` |
| Composite | [docs/Composite.md](../Composite.md) | `docs/spec/Composite.md` | `docs/design/Composite.md` |
| Factory Registry | [docs/FactoryRegistry.md](../FactoryRegistry.md) | `docs/spec/FactoryRegistry.md` | `docs/design/FactoryRegistry.md` |
| Decorator | [docs/Decorator.md](../Decorator.md) | `docs/spec/Decorator.md` | `docs/design/Decorator.md` |
| Event Aggregator | [docs/EventAggregator.md](../EventAggregator.md) | `docs/spec/EventAggregator.md` | `docs/design/EventAggregator.md` |
| State Transition Table | [docs/StateTransitionTable.md](../StateTransitionTable.md) | `docs/spec/StateTransitionTable.md` | `docs/design/StateTransitionTable.md` |

迁移可逐模式进行，不要求一次性完成。每个模式的迁移创建一个独立 PR。
