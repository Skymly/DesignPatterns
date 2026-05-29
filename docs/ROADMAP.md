# 产品与技术路线图

本文档记录**非发布**场景下的功能 backlog（不含许可证、NuGet 发布流程）。实施状态见 [`DEVELOPMENT.md`](DEVELOPMENT.md) 与 [`AGENTS.md`](../AGENTS.md)。

## 命名规则（生成器）

对契约接口 `IMyContract`（或类名）：

| 模式 | 生成类型示例 |
|------|----------------|
| Strategy | `MyContractKeys`、`MyContractRegistry` |
| Chain | `{Context}HandlerPipeline` |
| Composite | `MyContractCompositeKeys`、`MyContractCompositeCatalog.BuildRoot()` |
| Decorator | `{Contract}DecoratorStack.Build(core)`、`{Contract}DecoratorOrder` |
| Factory | `{Contract}Keys`、`{Contract}Registry` |

Decorator 设计详见 [Decorator.md](Decorator.md)。

---

## M2 — 模式能力

| 项 | 动机 | 依赖 | 预估 | 破坏性 |
|----|------|------|------|--------|
| EventAggregator | 轻量 pub/sub | 无 | 中 | 新 API |

## P3 — 生态与对称

| 项 | 动机 | 依赖 | 预估 | 破坏性 |
|----|------|------|------|--------|
| DI 扩展包 | 容器管理生命周期，替代静态 `new()` | 独立包项目 | 大 | 新包 |
| `[RegisterFactory]` | Factory 编译期 key/重复检测 | `DiagnosticIds` 稳定 | 中 | 新诊断 |
| `FrozenDictionary` | net8.0 `StrategyRegistry` 查找优化 | 多目标测试 | 小 | 无 |
| `IReadOnlyRegistry<TKey,TValue>` | Strategy/Factory 共享抽象 | API 评审 | 中 | 可能 |

## 已有模式增强（可选）

| 项 | 说明 |
|----|------|
| Handler `AllowMultiple` | 单类多 `TContext` |
| Composite 多根/森林 | v2 设计 |
| Composite 手动 `CompositeTreeBuilder` | Sample 分支演示 |
| CompletionProvider | IDE 补全，维护成本高 |

## 已完成（工程化）

- M2 EventAggregator：`IEventAggregator`、`IEventHandler<T>`、`EventAggregator` 实现、10 个单元测试、Sample
- P3 RegisterFactory：`[RegisterFactory]` 属性 + `RegisterFactoryGenerator`、DP020–022、集成测试、Sample
- P3 IReadOnlyRegistry：`IReadOnlyRegistry<TKey,TValue>` 共享抽象，`IStrategyRegistry`/`IFactoryRegistry` 均继承
- P3 FrozenDictionary：`StrategyRegistry` 在 net8.0 使用 `FrozenDictionary` 优化查找
- P3 DI 扩展包：`DesignPatterns.Extensions.DependencyInjection`、4 个扩展方法、22 个测试
- M2 Decorator：`IDecorator`、`DecoratorStackBuilder`、`[Decorator]` 生成器、DP016–019、Sample
- P0–P2：Factory 文档/示例、集成测试、CI、Analyzers/CodeFix 拆分、`DesignPatterns.Diagnostics`
- CodeFix：无参构造、接口实现、RegisterStrategy、ICompositeBuildable
- DP006 未注册策略 Analyzer

## 明确不做

- 23 种 GoF 全量框架化
- AppDomain 反射扫描注册
- Composite DP010–012 自动 CodeFix（结构错误需人工改）
