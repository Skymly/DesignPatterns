# AGENTS.md

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 项目是什么

**DesignPatterns** 是一个 .NET 设计模式工具库：

- **运行时**：轻量、可组合的 primitives（责任链、策略、工厂注册表、Singleton 特性等），组合优于继承。
- **编译期**：`DesignPatterns.SourceGenerators` 源生成器；`DesignPatterns.Analyzers` 诊断；`DesignPatterns.CodeFixes` CodeFix。

不要把它做成「23 种模式的厚重框架」或 MediatR/Polly 的替代品。

## 仓库结构

```
DesignPatterns.slnx
├── DesignPatterns/                    # 运行时核心（netstandard2.0 + net8.0）
├── DesignPatterns.Diagnostics/        # DiagnosticIds 常量（DP001–DP023）
├── DesignPatterns.SourceGenerators/   # 增量源生成器
├── DesignPatterns.Analyzers/          # DP006、DP023 Analyzer
├── DesignPatterns.CodeFixes/          # CodeFixProvider（Workspaces）
├── DesignPatterns.Extensions.DependencyInjection/ # DI 扩展包
├── DesignPatterns.Package/            # NuGet 元包（本地 pack，无发布计划）
├── tests/                             # Tests / SourceGenerators.Tests / Analyzers.Tests / DI.Tests
├── samples/                           # 各模式示例
├── docs/                              # DEVELOPMENT、ROADMAP、模式文档
└── AGENTS.md
```

规划但**尚未实现**：CompletionProvider。

## 常用命令

```bash
dotnet restore DesignPatterns.slnx
dotnet build DesignPatterns.slnx
dotnet test DesignPatterns.slnx
```

## 已实现的模式（摘要）

| 模式 | 特性 / API | 生成器 |
|------|------------|--------|
| Singleton | `[GenerateSingleton]` | `GenerateSingletonGenerator` |
| Factory Registry | `IFactoryRegistry`、`FactoryRegistryBuilder`、`[RegisterFactory]` | `RegisterFactoryGenerator` |
| Strategy | `[RegisterStrategy]` | `RegisterStrategyGenerator` |
| Chain | `IHandler<T>`、`HandlerPipeline` | `HandlerOrderGenerator` |
| Composite | `[CompositePart]` | `CompositePartGenerator` |
| Decorator | `[Decorator]`、`DecoratorStackBuilder` | `DecoratorGenerator` |
| EventAggregator | `IEventAggregator`、`IEventHandler<T>` | 无 |

模式文档：[docs/Strategy.md](docs/Strategy.md)、[docs/ChainOfResponsibility.md](docs/ChainOfResponsibility.md)、[docs/Composite.md](docs/Composite.md)、[docs/FactoryRegistry.md](docs/FactoryRegistry.md)、[docs/Decorator.md](docs/Decorator.md)、[docs/EventAggregator.md](docs/EventAggregator.md)。

## 编译期诊断 ID

| ID | 说明 |
|----|------|
| DP001–DP002 | GenerateSingleton |
| DP003–DP004、DP007 | RegisterStrategy（生成器） |
| DP005、DP008–DP009 | HandlerOrder（生成器） |
| DP006 | 未注册策略（Analyzer） |
| DP023 | 未注册工厂（Analyzer） |
| DP010–DP015 | CompositePart（生成器） |
| DP016–DP019 | Decorator（生成器） |
| DP020–DP022 | RegisterFactory（生成器） |

常量定义：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](DesignPatterns.Diagnostics/DiagnosticIds.cs)。规则表：[`AnalyzerReleases.Unshipped.md`](DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md)。

## 当前里程碑

| 阶段 | 内容 | 状态 |
|------|------|------|
| M0 | 文档与仓库骨架 | 已完成 |
| M1 | Chain、Strategy、Factory Registry 运行时 + 单元测试 | 已完成 |
| R1 | `[RegisterStrategy]` + DP003/004/007 | 已完成 |
| R1+ | Singleton、HandlerOrder 生成器 | 已完成 |
| M2 | Composite | 已完成 |
| M2 | Decorator | 已完成 |
| M2 | EventAggregator | 已完成 |
| P3 | `[RegisterFactory]` + DP020–022 | 已完成 |
| P3 | IReadOnlyRegistry + FrozenDictionary | 已完成 |
| P3 | DI 扩展包 | 已完成 |
| — | CodeFix + DP006/DP023 | 已完成 |

## 测试要求

| 层级 | 项目 |
|------|------|
| 单元 / 集成 | `tests/DesignPatterns.Tests` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests` |
| Analyzer / CodeFix | `tests/DesignPatterns.Analyzers.Tests` |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests` |

提交前：`dotnet build DesignPatterns.slnx` 与 `dotnet test DesignPatterns.slnx`。

详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 与用户沟通

- **最小 diff**、匹配现有风格、不主动 commit。
- 中文解释权衡；代码标识符可用英文。

## 待用户决策项（非阻塞开发）

- 开源许可证与公开发布（当前无计划）
- NuGet 元包是否同时打包 `net8.0` 运行时

功能 backlog：[docs/ROADMAP.md](docs/ROADMAP.md)。
