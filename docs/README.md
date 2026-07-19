# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。面向库使用者的英文/中文指南见 [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs)（VitePress 站点：[skymly.github.io/DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/)）。

> **文档约定**：[DOCUMENTATION.md](DOCUMENTATION.md) — ADR / Design Doc / Roadmap；任务与审查用 GitHub Issue / PR。

## 入门

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | 文档约定 |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 环境、构建、测试、架构约定 |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程 |
| [PUBLISHING.md](PUBLISHING.md) | NuGet 发版流程 |
| [ROADMAP.md](ROADMAP.md) | 功能与技术 backlog |
| [../AGENTS.md](../AGENTS.md) | AI 编码助手上下文（诊断 ID 权威登记） |

## 决策与设计

| 目录 | 说明 |
|------|------|
| [adr/](adr/README.md) | ADR — 架构决策记录 |
| [design/](design/README.md) | Design Doc — 每域 API / 诊断 / 实现 |

## 模式索引

| 模式 | Design Doc |
|------|------------|
| Strategy | [design/Strategy.md](design/Strategy.md) |
| Chain of Responsibility | [design/ChainOfResponsibility.md](design/ChainOfResponsibility.md) |
| Composite | [design/Composite.md](design/Composite.md) |
| Factory Registry | [design/FactoryRegistry.md](design/FactoryRegistry.md) |
| Decorator | [design/Decorator.md](design/Decorator.md) |
| Event Aggregator | [design/EventAggregator.md](design/EventAggregator.md) |
| State Transition Table | [design/StateTransitionTable.md](design/StateTransitionTable.md) |

## 横切约定

| 文档 | 说明 |
|------|------|
| [FactoryKeyConventions.md](FactoryKeyConventions.md) | Strategy / Factory 字符串 key 命名与复合 key（`outer:inner`） |

## DI 容器扩展

| 文档 | 说明 |
|------|------|
| [Autofac.md](Autofac.md) | Autofac `RegisterAutofac` 源生成器集成 |
| [Configuration.md](Configuration.md) | `IConfiguration` → `IStrategyRegistry` 桥接 |
| [PluginAssemblies.md](PluginAssemblies.md) | 卫星程序集 + 多项目 Registry 约定 |

用户向精简版见 DesignPatterns.Docs：[registry-key-conventions](https://skymly.github.io/DesignPatterns.Docs/registry-key-conventions)。

## 与用户文档站的分工

| 受众 | 位置 | 语言 |
|------|------|------|
| 库使用者 | `DesignPatterns.Docs` 仓库 | 英文为主 + 中文镜像 |
| 维护者 / 深度 API | 本目录 `docs/` | 中文设计说明 |
| 跨工具 AI 约束 | 根目录 `AGENTS.md` | 中文 |

修改诊断 ID、CodeFix 或公共 API 时，同步更新：`DiagnosticIds.cs`、对应 Design Doc、`DesignPatterns.Docs` 的 [diagnostics](https://github.com/Skymly/DesignPatterns.Docs/blob/main/docs/diagnostics.md) 页，以及（若影响 key 约定）本目录 [FactoryKeyConventions.md](FactoryKeyConventions.md)。
