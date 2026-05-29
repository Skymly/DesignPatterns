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

## 已有模式增强（可选）

| 项 | 说明 |
|----|------|
| Handler `AllowMultiple` | 单类多 `TContext` |
| Composite 多根/森林 | v2 设计 |
| Composite 手动 `CompositeTreeBuilder` | Sample 分支演示 |
| CompletionProvider | IDE 补全，维护成本高 |

## 已完成（工程化）

- M2 EventAggregator：`IEventAggregator`、`IEventHandler<T>`、`EventAggregator` 实现、单元测试、Sample
- P3 RegisterFactory：`[RegisterFactory]` 属性 + `RegisterFactoryGenerator`、DP020–022、集成测试
- P3 IReadOnlyRegistry：`IReadOnlyRegistry<TKey,TValue>`；**仅** `IStrategyRegistry` 继承（`IFactoryRegistry` 不继承）
- P3 FrozenDictionary：`StrategyRegistry` 在 net8.0 使用 `FrozenDictionary` 优化查找
- P3 DI 扩展包：`DesignPatterns.Extensions.DependencyInjection`、`AddEventAggregator` 等
- M2 Decorator：`IDecorator`、`DecoratorStackBuilder`、`[Decorator]` 生成器、DP016–019、Sample
- P0–P2：Factory 文档/示例、集成测试、CI、Analyzers/CodeFix 拆分、`DesignPatterns.Diagnostics`
- CodeFix：无参构造、接口实现、RegisterStrategy、RegisterFactory、ICompositeBuildable
- DP006 未注册策略 Analyzer；DP023 未注册工厂 Analyzer

## 明确不做

- 23 种 GoF 全量框架化
- AppDomain 反射扫描注册
- Composite DP010–012 自动 CodeFix（结构错误需人工改）
