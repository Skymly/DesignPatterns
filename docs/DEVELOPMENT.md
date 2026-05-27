# 开发文档

本文档说明如何在本地搭建环境、构建项目、参与贡献，以及当前阶段的架构与路线图。

## 环境要求

| 工具 | 版本建议 |
|------|----------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 或更高（仓库当前目标 `netstandard2.0`，后续将增加 `net8.0` 多目标） |
| Git | 2.x |
| IDE | Visual Studio 2022、Rider 或 VS Code + C# Dev Kit |

可选（实现 Roslyn 组件时）：

- Visual Studio 17.x（用于调试分析器与 Completion）
- `dotnet format`（代码风格统一）

## 克隆与构建

```bash
git clone <your-remote-url> DesignPatterns
cd DesignPatterns
dotnet restore DesignPatterns.slnx
dotnet build DesignPatterns.slnx --configuration Release
```

仅构建核心库：

```bash
dotnet build DesignPatterns/DesignPatterns.csproj
```

## 仓库布局约定

### 当前

- `DesignPatterns/` — 运行时 NuGet 包根项目
- `DesignPatterns.slnx` — 解决方案入口
- `docs/` — 人类可读的设计与开发说明
- `AGENTS.md` — AI 助手专用上下文（与本文档互补，更偏「操作清单」）

### 规划（按里程碑引入）

| 路径 | 职责 |
|------|------|
| `DesignPatterns.Analyzers/` | `IIncrementalGenerator`、`DiagnosticAnalyzer`、`CodeFixProvider`、`CompletionProvider` |
| `tests/DesignPatterns.Tests/` | 运行时 API 单元测试 |
| `tests/DesignPatterns.Analyzers.Tests/` | 生成器/分析器快照与诊断测试 |
| `samples/` | 每个模式一个最小可运行示例，兼作文档 |

命名空间根：`DesignPatterns`（运行时）、`DesignPatterns.Analyzers`（编译期）。

## 架构原则

### 运行时库

1. **Primitives，非框架**：暴露接口、管道、注册表；不替用户做领域建模。
2. **零/极少依赖**：Core 不引用 `Microsoft.Extensions.DependencyInjection`；DI 扩展放在独立包（规划中）。
3. **异步一等**：管道与处理器默认支持 `CancellationToken` 与 `ValueTask`。
4. **显式失败**：优先 `TryResolve`、明确异常信息，避免静默 `null`。

### Roslyn 组件

1. 使用 **`IIncrementalGenerator`**，通过 `ForAttributeWithMetadataName` 绑定特性。
2. 分析器程序集：`IsRoslynComponent`、`EnforceExtendedAnalyzerRules`、`IncludeBuildOutput=false`。
3. **生成胶水代码**：注册表、排序管道、`StrategyKeys` 常量；不生成完整业务状态机。
4. 诊断 ID 统一前缀 **`DP`**（如 `DP002`：实现策略但未注册）。
5. IDE 体验优先级：**生成强类型常量 > CodeFix > Snippet > 自定义 Completion**。

### 与生态边界

- 不重复实现 MediatR、Polly、`Microsoft.Extensions.ObjectPool` 的完整能力；文档中说明何时引用官方包。
- Singleton 不作为卖点；诊断可提示静态可变单例，推荐 DI 生命周期。

## 设计模式实施优先级

### 运行时（M1 建议）

1. [x] Chain of Responsibility — `IHandler<TContext>` + 管道（见 [ChainOfResponsibility.md](ChainOfResponsibility.md)）
2. [x] Strategy — 命名注册与解析（见 [Strategy.md](Strategy.md)）
3. [x] Factory Registry — 键到实现的映射

### Roslyn（R1 建议）

1. `[RegisterStrategy]` + 增量生成注册表
2. 诊断：未注册实现、重复 key
3. CodeFix：一键添加注册特性

### 后续（M2）

- [x] Composite — `ICompositeNode<T>` + 遍历 + `[CompositePart]`（见 [Composite.md](Composite.md)）
- [ ] Decorator 链
- [ ] 轻量 EventAggregator
- Handler `[HandlerOrder]` 生成（已实现，见 [ChainOfResponsibility.md](ChainOfResponsibility.md)）

## 编码规范

- 启用 **nullable reference types**（新文件与项目属性逐步开启）。
- 公共 API 配备 **XML 文档注释**（`GenerateDocumentationFile`）。
- 遵循 [.NET 设计指南](https://learn.microsoft.com/dotnet/standard/design-guidelines/)：接口以 `I` 开头，异步方法以 `Async` 结尾。
- 每个公共类型归属清晰命名空间，按 GoF 分类子命名空间（如 `DesignPatterns.Behavioral`）在类型增多后引入，避免过早分层。

## 测试

```bash
dotnet test DesignPatterns.slnx
```

运行时测试：`tests/DesignPatterns.Tests`；生成器快照：`tests/DesignPatterns.SourceGenerators.Tests`（Verify）。

## 提交与分支

- 默认分支：`main`
- 功能分支：`feature/<short-description>`
- 修复分支：`fix/<short-description>`
- 提交信息：祈使句、英文或中文均可，需说明 **why**（例：`Add RegisterStrategy source generator for strategy registry`）

尚未配置 CI 时，提交前请在本地执行 `dotnet build`（后续补充 `dotnet test`）。

## 发布（规划）

- 运行时：`DesignPatterns` NuGet 包
- 编译期：`DesignPatterns.Analyzers` 或作为运行时包的 analyzer 依赖（待决）
- 版本： [Semantic Versioning 2.0.0](https://semver.org/)

## 相关文档

- [AGENTS.md](../AGENTS.md) — AI 助手在本仓库中的职责、命令与禁忌
- 对话中形成的架构备忘：模块化包、Roslyn 三件套协同流程（见 AGENTS.md 架构摘要）

## 待决事项

- [ ] 开源许可证（MIT / Apache-2.0）
- [ ] 分析器与主包是否同 NuGet 发布
- [ ] 多目标框架：`netstandard2.0` + `net8.0`
- [ ] CI（GitHub Actions）：build + test + 包验证
