# DesignPatterns



[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)



面向 .NET 的**设计模式工具库**：提供可组合的运行时 primitives，并通过 Roslyn 源生成器在编译期减少样板代码、发现误用。



## 项目状态（早期阶段）



> **本仓库已开源（[MIT](LICENSE)），但处于早期开发阶段。**

>

> - **公共 API 尚未稳定**：类型、特性、源生成器产出、诊断 ID（`DP###`）及命名规则可能在任意版本变更，**不保证**语义化版本下的向后兼容。

> - **NuGet 发布**：元包 **`Skymly.DesignPatterns`** **`0.1.0-preview4`** 发布至 [nuget.org](https://www.nuget.org/packages/Skymly.DesignPatterns) 与 GitHub Packages；亦支持本地 `./build.ps1 --target CiPack`。C# 命名空间仍为 `DesignPatterns.*`；旧 GitHub 包 ID `DesignPatterns`（preview1/2）已弃用。

> - **生产环境**：建议仅在可接受 API 变动的实验、内部工具或学习场景使用；在 API 稳定前请固定 commit 或 fork，并关注 [ROADMAP](docs/ROADMAP.md) 与 breaking changes。



**English:** This project is open source under the MIT license and in an **early preview**. Public APIs, generated code shapes, and diagnostics are **not stable** yet. Do not assume SemVer compatibility until a stability announcement.



反馈与贡献见 [CONTRIBUTING.md](CONTRIBUTING.md)（Issue / PR 建议使用英语，见仓库协作约定）。



## 目标



- 帮助开发者在真实项目中**更快、更一致**地应用常见设计模式

- **组合优于继承**：轻量接口与扩展，而非厚重基类体系

- **编译期与运行时协同**：特性标记 + 源生成 + 诊断，而非魔法框架



## 已实现的模式



| 模式 | 命名空间 | 运行时 | 源生成器 |

|------|----------|--------|----------|

| Singleton | `DesignPatterns.Creational` | `[GenerateSingleton]` | `Lazy<T>` + `Instance` |

| Factory Registry | `DesignPatterns.Creational` | `IFactoryRegistry` / `FactoryRegistryBuilder` | `[RegisterFactory]` → Keys + Registry |

| Strategy | `DesignPatterns.Behavioral` | `IStrategyRegistry` / Builder / `ExecuteAsync` | `[RegisterStrategy]` → Keys + Registry |

| Chain of Responsibility | `DesignPatterns.Behavioral` | `IHandler<T>` / `HandlerPipeline` / `InvokeTracedAsync` | `[HandlerOrder]` → `{Context}HandlerPipeline` |

| Composite | `DesignPatterns.Structural` | `ICompositeNode<T>` / `CompositeTraverser`（`Traverse` / `TraverseForest`） | `[CompositePart]` → Keys + Catalog + `BuildRoot()` / `BuildForest()` |

| Decorator | `DesignPatterns.Structural` | `IDecorator<T>` / `DecoratorStackBuilder`（条件 `Add`） | `[Decorator]` → `{Contract}DecoratorStack` + `{Contract}DecoratorOrder` |

| Event Aggregator | `DesignPatterns.Behavioral` | `IEventAggregator` / `IEventHandler<T>` | — |



另可选 **`DesignPatterns.Extensions.DependencyInjection`**：手动 `AddStrategyRegistry` / `AddFactoryRegistry` / `AddHandlerPipeline`，以及引用该扩展项目/包时由源生成器输出的 **`{Contract}Registry.RegisterDi(services)`**（Strategy / Factory / Handler 从容器解析，见 [docs/Strategy.md](docs/Strategy.md)）。当前元包 **`Skymly.DesignPatterns`** 不包含该 DI 扩展；独立 NuGet 发布策略待 API 与发版流程稳定后再确定。Factory 另支持 `[RegisterFactory]` 源生成器（见 [docs/FactoryRegistry.md](docs/FactoryRegistry.md)）。



本地或 CI 打包后，元包 **`Skymly.DesignPatterns`** 可同时引用运行时库与源生成器（见下方「快速开始」）。正式发布到 NuGet 前请以 README 中的项目状态为准。



## 仓库结构



```

DesignPatterns.slnx

├── DesignPatterns/                    # 运行时核心（netstandard2.0 + net8.0）

├── DesignPatterns.Diagnostics/        # DP### 诊断 ID 常量

├── DesignPatterns.SourceGenerators/   # 增量源生成器

├── DesignPatterns.Analyzers/          # DP006、DP023、DP024、DP025 Analyzer

├── DesignPatterns.CodeFixes/          # CodeFix（含 DP024 HandlerOrder）

├── DesignPatterns.Package/            # NuGet 元包（PackageId=Skymly.DesignPatterns）

├── tests/DesignPatterns.Tests/        # 运行时单元测试（xUnit）

├── tests/DesignPatterns.SourceGenerators.Tests/  # 生成器 Verify 快照

├── tests/DesignPatterns.Analyzers.Tests/         # Analyzer/CodeFix 测试

└── docs/                              # 设计与开发文档

```



## 生态仓库



| 仓库 | 说明 |

|------|------|

| [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs) | 用户文档站（VitePress，[GitHub Pages](https://skymly.github.io/DesignPatterns.Docs/)） |

| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | 可运行示例（9 个控制台项目；主仓 CI 会 checkout 并 build + run） |



本地工作区建议并列克隆：`DesignPatterns/`、`DesignPatterns.Samples/`、`DesignPatterns.Docs/`。



## 文档



| 文档 | 说明 |

|------|------|

| [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs) | 面向用户的英文/中文指南（模式、诊断、快速开始） |

| [docs/README.md](docs/README.md) | 内部文档导航（维护者） |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 环境、构建、测试、架构约定 |
| [docs/PUBLISHING.md](docs/PUBLISHING.md) | NuGet 发版流程 |
| [docs/FactoryKeyConventions.md](docs/FactoryKeyConventions.md) | Strategy / Factory key 命名约定 |

| [docs/Strategy.md](docs/Strategy.md) | Strategy 模式设计与 API |

| [docs/ChainOfResponsibility.md](docs/ChainOfResponsibility.md) | 责任链模式设计与 API |

| [docs/Composite.md](docs/Composite.md) | Composite 模式设计与 API |

| [docs/FactoryRegistry.md](docs/FactoryRegistry.md) | Factory Registry 模式设计与 API |

| [docs/Decorator.md](docs/Decorator.md) | Decorator 模式设计与 API |

| [docs/EventAggregator.md](docs/EventAggregator.md) | Event Aggregator 模式设计与 API |

| [docs/ROADMAP.md](docs/ROADMAP.md) | 功能与技术 backlog |

| [CONTRIBUTING.md](CONTRIBUTING.md) | 贡献与测试说明 |

| [AGENTS.md](AGENTS.md) | AI 编码助手项目上下文 |



## 快速开始



### NuGet（预览，`Skymly.DesignPatterns` `0.1.0-preview4`）



```xml
<PackageReference Include="Skymly.DesignPatterns" Version="0.1.0-preview4" />
```



默认从 [nuget.org](https://api.nuget.org/v3/index.json) 还原。GitHub Packages 镜像（可选）：



```xml
<packageSources>
  <add key="github" value="https://nuget.pkg.github.com/Skymly/index.json" />
</packageSources>
```



### 源码



```bash

git clone https://github.com/Skymly/DesignPatterns.git

cd DesignPatterns

```



```powershell

# 与 CI 相同（Nuke）

./build.ps1 --target Ci --configuration Release

```



打包并校验 NuGet 元包（输出到 `artifacts/package/`）：



```powershell

./build.ps1 --target CiPack --configuration Release

```



### 示例程序



可运行示例在独立仓库 [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples)（默认 sibling 引用 `../DesignPatterns`）：



```powershell

git clone https://github.com/Skymly/DesignPatterns.git

git clone https://github.com/Skymly/DesignPatterns.Samples.git

cd DesignPatterns.Samples

./build.ps1 --target Ci --configuration Release

```



详见 [DesignPatterns.Docs — Samples](https://github.com/Skymly/DesignPatterns.Docs/blob/main/docs/samples.md)。



## 规划中的能力



见 [docs/ROADMAP.md](docs/ROADMAP.md)（可选增强项与 CompletionProvider 等）。
