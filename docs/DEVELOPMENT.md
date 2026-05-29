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
| `DesignPatterns.Diagnostics/` | 诊断 ID 常量 |
| `DesignPatterns.Analyzers/` | `DiagnosticAnalyzer`（DP006、DP023） |
| `DesignPatterns.CodeFixes/` | `CodeFixProvider` |
| `tests/DesignPatterns.Tests/` | 运行时 API 单元测试 |
| `tests/DesignPatterns.SourceGenerators.Tests/` | 生成器 Verify 快照与诊断测试 |
| `tests/DesignPatterns.Analyzers.Tests/` | Analyzer/CodeFix 测试（P2 起） |
| `samples/` | 每个模式一个最小可运行示例，兼作文档 |

命名空间根：`DesignPatterns`（运行时）、`DesignPatterns.Analyzers`（编译期）。

## 架构原则

### 运行时库

1. **Primitives，非框架**：暴露接口、管道、注册表；不替用户做领域建模。
2. **零/极少依赖**：Core 不引用 `Microsoft.Extensions.DependencyInjection`；DI 扩展在 `DesignPatterns.Extensions.DependencyInjection`。
3. **异步一等**：管道与处理器默认支持 `CancellationToken` 与 `ValueTask`。
4. **显式失败**：优先 `TryResolve`、明确异常信息，避免静默 `null`。

### Roslyn 组件

1. 使用 **`IIncrementalGenerator`**，通过 `ForAttributeWithMetadataName` 绑定特性。
2. 分析器程序集：`IsRoslynComponent`、`EnforceExtendedAnalyzerRules`、`IncludeBuildOutput=false`。
3. **生成胶水代码**：注册表、排序管道、`StrategyKeys` 常量；不生成完整业务状态机。
4. 诊断 ID 统一前缀 **`DP`**（如 `DP002`：`GenerateSingleton` 无效目标；`DP006`：策略实现未注册）。
5. IDE 体验优先级：**生成强类型常量 > CodeFix > Snippet > 自定义 Completion**。

### 与生态边界

- 不重复实现 MediatR、Polly、`Microsoft.Extensions.ObjectPool` 的完整能力；文档中说明何时引用官方包。
- Singleton 不作为卖点；诊断可提示静态可变单例，推荐 DI 生命周期。

## 设计模式实施优先级

### 运行时（M1 建议）

1. [x] Chain of Responsibility — `IHandler<TContext>` + 管道（见 [ChainOfResponsibility.md](ChainOfResponsibility.md)）
2. [x] Strategy — 命名注册与解析（见 [Strategy.md](Strategy.md)）
3. [x] Factory Registry — 键到实现的映射（见 [FactoryRegistry.md](FactoryRegistry.md)）

### Roslyn（R1 建议）

1. [x] `[RegisterStrategy]` + 增量生成注册表
2. [x] 诊断：重复 key、接口不匹配、缺无参构造（DP003/004/007）
3. [x] `[HandlerOrder]` 生成（见 [ChainOfResponsibility.md](ChainOfResponsibility.md)）
4. [x] CodeFix + DP006 未注册策略 Analyzer（`DesignPatterns.Analyzers`）

### 后续（M2）

- [x] Composite — `ICompositeNode<T>` + 遍历 + `[CompositePart]`（见 [Composite.md](Composite.md)）
- [x] Decorator — 服务包装栈（见 [Decorator.md](Decorator.md)）
- [x] EventAggregator — 轻量 pub/sub（`IEventAggregator`、`IEventHandler<T>`）

### 生态（P3）

- [x] `[RegisterFactory]` — Factory 编译期 key/重复检测（DP020–022）
- [x] `IReadOnlyRegistry<TKey,TValue>` — 仅 `IStrategyRegistry` 继承
- [x] `FrozenDictionary` — net8.0 `StrategyRegistry` 查找优化
- [x] DI 扩展包 — `DesignPatterns.Extensions.DependencyInjection`

## 编码规范

- 启用 **nullable reference types**（新文件与项目属性逐步开启）。
- 公共 API 配备 **XML 文档注释**（`GenerateDocumentationFile`）。
- 遵循 [.NET 设计指南](https://learn.microsoft.com/dotnet/standard/design-guidelines/)：接口以 `I` 开头，异步方法以 `Async` 结尾。
- 每个公共类型归属清晰命名空间，按 GoF 分类子命名空间（如 `DesignPatterns.Behavioral`）在类型增多后引入，避免过早分层。

## 测试

```bash
dotnet test DesignPatterns.slnx
```

### 测试金字塔

| 层级 | 项目 | 覆盖 |
|------|------|------|
| 单元 | `tests/DesignPatterns.Tests` | 运行时 API、特性校验 |
| 集成 | `tests/DesignPatterns.Tests/Integration/` | 生成器产出 → 运行时（Chain/Strategy/Composite/Singleton/Decorator/Factory/EventAggregator 单元测试在 Behavioral） |
| 快照 | `tests/DesignPatterns.SourceGenerators.Tests` | 生成源码与 DP 诊断（Verify） |
| Analyzer | `tests/DesignPatterns.Analyzers.Tests` | DP006、DP023、CodeFix（Strategy/Factory/Handler/Composite） |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests` | DI 注册与解析 |

```bash
dotnet test DesignPatterns.slnx
```

贡献流程见 [CONTRIBUTING.md](../CONTRIBUTING.md)。产品 backlog 见 [ROADMAP.md](ROADMAP.md)。

## 提交与分支

- 默认分支：`main`
- 功能分支：`feature/<short-description>`
- 修复分支：`fix/<short-description>`
- 提交信息：祈使句、英文或中文均可，需说明 **why**（例：`Add RegisterStrategy source generator for strategy registry`）

CI：GitHub Actions [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)（`main` push/PR：restore、Release build、test、samples 编译）。

## 相关文档

- [AGENTS.md](../AGENTS.md) — AI 助手上下文
- [CONTRIBUTING.md](../CONTRIBUTING.md) — 贡献与测试流程
- [ROADMAP.md](ROADMAP.md) — 功能 backlog
- [Decorator.md](Decorator.md) — Decorator 模式设计与 API
- [EventAggregator.md](EventAggregator.md) — Event Aggregator 模式设计与 API

## 待决事项（无发布计划时可延后）

- [ ] 开源许可证
- [ ] 公开发布与 SemVer 流程
- [ ] NuGet 元包 `net8.0` 运行时 TFM
