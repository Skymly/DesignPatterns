# DesignPatterns

面向 .NET 的**设计模式工具库**：提供可组合的运行时 primitives，并通过 Roslyn 源生成器在编译期减少样板代码、发现误用。

## 目标

- 帮助开发者在真实项目中**更快、更一致**地应用常见设计模式
- **组合优于继承**：轻量接口与扩展，而非厚重基类体系
- **编译期与运行时协同**：特性标记 + 源生成 + 诊断，而非魔法框架

## 已实现的模式

| 模式 | 命名空间 | 运行时 | 源生成器 |
|------|----------|--------|----------|
| Singleton | `DesignPatterns.Creational` | `[GenerateSingleton]` | `Lazy<T>` + `Instance` |
| Factory Registry | `DesignPatterns.Creational` | `IFactoryRegistry` / `FactoryRegistryBuilder` | — |
| Strategy | `DesignPatterns.Behavioral` | `IStrategyRegistry` / Builder | `[RegisterStrategy]` → Keys + Registry |
| Chain of Responsibility | `DesignPatterns.Behavioral` | `IHandler<T>` / `HandlerPipeline` | `[HandlerOrder]` → `{Context}HandlerPipeline` |
| Composite | `DesignPatterns.Structural` | `ICompositeNode<T>` / `CompositeTraverser` | `[CompositePart]` → Keys + Catalog + `BuildRoot()` |
| Decorator | `DesignPatterns.Structural` | `IDecorator<T>` / `DecoratorStackBuilder` | `[Decorator]` → `{Contract}DecoratorStack.Build` |

安装 NuGet 包 **`DesignPatterns`**（元包）即可同时获得运行时库与源生成器。

## 仓库结构

```
DesignPatterns.slnx
├── DesignPatterns/                    # 运行时核心（netstandard2.0 + net8.0）
├── DesignPatterns.Diagnostics/        # DP### 诊断 ID 常量
├── DesignPatterns.SourceGenerators/   # 增量源生成器
├── DesignPatterns.Analyzers/          # DP006 Analyzer
├── DesignPatterns.CodeFixes/          # CodeFix
├── DesignPatterns.Package/            # NuGet 元包（PackageId=DesignPatterns）
├── tests/DesignPatterns.Tests/        # 运行时单元测试（xUnit）
├── tests/DesignPatterns.SourceGenerators.Tests/  # 生成器 Verify 快照
├── tests/DesignPatterns.Analyzers.Tests/         # Analyzer/CodeFix 测试
├── samples/                           # 按模式划分的示例
│   ├── GenerateSingleton.Sample/
│   ├── Strategy.Sample/
│   ├── Chain.Sample/
│   ├── Composite.Sample/
│   ├── Factory.Sample/
│   └── Decorator.Sample/
└── docs/                              # 设计与开发文档
```

## 文档

| 文档 | 说明 |
|------|------|
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 环境、构建、测试、架构约定 |
| [docs/Strategy.md](docs/Strategy.md) | Strategy 模式设计与 API |
| [docs/ChainOfResponsibility.md](docs/ChainOfResponsibility.md) | 责任链模式设计与 API |
| [docs/Composite.md](docs/Composite.md) | Composite 模式设计与 API |
| [docs/FactoryRegistry.md](docs/FactoryRegistry.md) | Factory Registry 模式设计与 API |
| [docs/Decorator.md](docs/Decorator.md) | Decorator 模式设计与 API |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 功能与技术 backlog |
| [CONTRIBUTING.md](CONTRIBUTING.md) | 贡献与测试说明 |
| [AGENTS.md](AGENTS.md) | AI 编码助手项目上下文 |

## 快速开始

```bash
git clone https://github.com/Skymly/DesignPatterns.git
cd DesignPatterns
dotnet build DesignPatterns.slnx
dotnet test DesignPatterns.slnx
```

打包 NuGet（本地）：

```bash
dotnet pack DesignPatterns.Package/DesignPatterns.Package.csproj -c Release -o artifacts/packages
```

## 规划中的能力

见 [docs/ROADMAP.md](docs/ROADMAP.md)（EventAggregator、DI 扩展、`[RegisterFactory]` 等）。
