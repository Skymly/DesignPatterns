# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。面向库使用者的英文/中文指南见 [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs)（VitePress 站点：[skymly.github.io/DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/)）。

## 入门

| 文档 | 说明 |
|------|------|
| [DEVELOPMENT.md](DEVELOPMENT.md) | 环境、构建、测试、架构约定 |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程、测试与 Roslyn 组件结构 |
| [PUBLISHING.md](PUBLISHING.md) | NuGet 预览/正式发版流程 |
| [ROADMAP.md](ROADMAP.md) | 功能与技术 backlog |
| [../AGENTS.md](../AGENTS.md) | AI 编码助手上下文（诊断 ID 权威登记） |

## 模式设计（实现细节）

| 文档 | 模式 |
|------|------|
| [Strategy.md](Strategy.md) | Strategy + `[RegisterStrategy]` |
| [ChainOfResponsibility.md](ChainOfResponsibility.md) | 责任链 + `[HandlerOrder]` |
| [Composite.md](Composite.md) | Composite + `[CompositePart]` |
| [FactoryRegistry.md](FactoryRegistry.md) | Factory Registry + `[RegisterFactory]` |
| [Decorator.md](Decorator.md) | Decorator + `[Decorator]` |
| [EventAggregator.md](EventAggregator.md) | Event Aggregator |
| [StateTransitionTable.md](StateTransitionTable.md) | State 转换表 + `[StateMachine]` / `[Transition]` |

## 横切约定

| 文档 | 说明 |
|------|------|
| [FactoryKeyConventions.md](FactoryKeyConventions.md) | Strategy / Factory 字符串 key 命名与复合 key（`outer:inner`） |

## DI 容器扩展

| 文档 | 说明 |
|------|------|
| [Autofac.md](Autofac.md) | Autofac `RegisterAutofac` 源生成器集成 |
| [Configuration.md](Configuration.md) | AppSettings → `IStrategyRegistry` 桥接 |
| [PluginAssemblies.md](PluginAssemblies.md) | 卫星程序集 + 多项目 Registry 约定 |

## RFC（已落地）

| 文档 | 说明 |
|------|------|
| [rfc/StateTransitionTable.md](rfc/StateTransitionTable.md) | State 转换表 v1 设计记录（已实现于 0.1.0-preview4） |

用户向精简版见 DesignPatterns.Docs：[registry-key-conventions](https://skymly.github.io/DesignPatterns.Docs/registry-key-conventions)。

## 与用户文档站的分工

| 受众 | 位置 | 语言 |
|------|------|------|
| 库使用者 | `DesignPatterns.Docs` 仓库 | 英文为主 + 中文镜像 |
| 维护者 / 深度 API | 本目录 `docs/` | 中文设计说明 |
| 跨工具 AI 约束 | 根目录 `AGENTS.md` | 中文 |

修改诊断 ID、CodeFix 或公共 API 时，同步更新：`DiagnosticIds.cs`、`DesignPatterns.Docs` 的 [diagnostics](https://github.com/Skymly/DesignPatterns.Docs/blob/main/docs/diagnostics.md) 页，以及（若影响 key 约定）本目录 [FactoryKeyConventions.md](FactoryKeyConventions.md)。
