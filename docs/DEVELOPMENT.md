# 开发文档

本文档是**操作手册**（环境、构建、测试、架构）。**项目级开发规范的权威源是 [`../AGENTS.md`](../AGENTS.md)**（编码标准、兼容基线、打包、测试与覆盖率、诊断 ID、语言、版本与发布）；功能与工程 backlog 见 [`ROADMAP.md`](ROADMAP.md)。本文与上述文档冲突时，以 `AGENTS.md` 为准。

## 环境要求

| 工具 | 版本建议 |
|------|----------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 或更高（运行时多目标 `netstandard2.0` + `net8.0`，见 [`../AGENTS.md`](../AGENTS.md) 兼容基线） |
| Git | 2.x |
| IDE | Visual Studio 2022、Rider 或 VS Code + C# Dev Kit |

可选（实现 Roslyn 组件时）：

- Visual Studio 17.x（用于调试分析器与 Completion）
- `dotnet format`（代码风格统一）

## 克隆与构建

```bash
git clone https://github.com/Skymly/DesignPatterns.git
cd DesignPatterns
```

**推荐（与 CI 一致，Nuke）：**

```powershell
# Windows
./build.ps1 --target Ci --configuration Release
./build.ps1 --target CiPack --configuration Release

# Linux / macOS
./build.sh --target Ci --configuration Release
```

**传统 dotnet：**

```bash
dotnet restore DesignPatterns.slnx
dotnet build DesignPatterns.slnx --configuration Release
dotnet test DesignPatterns.slnx --configuration Release --no-build
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

### 已实现的模块

| 路径 | 职责 |
|------|------|
| `DesignPatterns.Diagnostics/` | 诊断 ID 常量 |
| `DesignPatterns.Analyzers/` | `DiagnosticAnalyzer`（DP006 / DP023 / DP024 / DP025） |
| `DesignPatterns.CodeFixes/` | `CodeFixProvider` |
| `DesignPatterns.Extensions.DependencyInjection/` | MSDI 扩展 + DI 生成器 targets |
| `DesignPatterns.Extensions.Configuration/` | `IConfiguration` → strategy registry 桥接 |
| `DesignPatterns.Package/` | NuGet 元包（`PackageId=Skymly.DesignPatterns`） |
| `tests/DesignPatterns.Tests/` | 运行时 API 单元测试 |
| `tests/DesignPatterns.SourceGenerators.Tests/` | 生成器 Verify 快照与诊断测试 |
| `tests/DesignPatterns.Analyzers.Tests/` | Analyzer / CodeFix 测试 |
| `tests/DesignPatterns.Extensions.DependencyInjection.Tests/` | DI 注册与解析测试 |
| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | 每个模式一个可运行控制台示例（含 `RegisterDi`）；主仓 CI checkout 并 build + run |

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

- 本库以**技术探索**为目的，**允许与 MediatR、Polly、`Microsoft.Extensions.ObjectPool`、Stateless 等生态库能力重叠**——重叠不是拒绝理由，能否在源生成 / 诊断 / DI 集成等编译期能力上做出探索价值才是判断标准（见 [`../AGENTS.md`](../AGENTS.md)「项目是什么」）。
- 模式文档中仍可说明与生态库的**差异点**（设计取向、编译期协同、primitive 粒度），作为用户选型参考，而非「不实现」的理由。
- Singleton 不作为卖点；诊断可提示静态可变单例，推荐 DI 生命周期。

## 设计模式实施情况（历史）

> 以下模式与生成器均**已落地**。后续功能 backlog 见 [ROADMAP.md](ROADMAP.md)，架构原则见 [`../AGENTS.md`](../AGENTS.md)。

### 运行时（M1）

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

### 包边界

当前可打包的 NuGet 入口是元包 `Skymly.DesignPatterns`（`DesignPatterns.Package/`），用于携带运行时核心、源生成器、Analyzer 与 CodeFix。`DesignPatterns.Extensions.DependencyInjection` 目前作为仓库内独立扩展项目存在，未由元包包含；是否独立发布为 NuGet 包留待 API 稳定与发版流程确定后决策。

## 编码规范

编码标准（nullable、file-scoped namespace、XML 文档、命名、GoF 子命名空间、warnings-as-errors）以 [`../AGENTS.md`](../AGENTS.md) 的「编码标准」「兼容基线」为权威源；风格细则见 [`../.editorconfig`](../.editorconfig)。本节不再重复，避免漂移。

## 测试

```powershell
./build.ps1 --target Ci --configuration Release
```

或：

```bash
dotnet test DesignPatterns.slnx -c Release
```

Nuke `UnitTest` 会依次运行 `tests/` 下四个测试项目，并将 TRX 写入 `TestResults/`。

### 测试金字塔

| 层级 | 项目 | 覆盖 |
|------|------|------|
| 单元 | `tests/DesignPatterns.Tests` | 运行时 API、特性校验 |
| 集成 | `tests/DesignPatterns.Tests/Integration/` | 生成器产出 → 运行时（Chain/Strategy/Composite/Singleton/Decorator/Factory/EventAggregator 单元测试在 Behavioral） |
| 快照 | `tests/DesignPatterns.SourceGenerators.Tests` | 生成源码与 DP 诊断（Verify） |
| Analyzer | `tests/DesignPatterns.Analyzers.Tests` | DP006、DP023、DP024、DP025、DP033 与 CodeFix（Strategy/Factory/Handler/Registry key） |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests` | DI 注册与解析 |
| Configuration 扩展 | `tests/DesignPatterns.Extensions.Configuration.Tests`（net8.0）、`tests/DesignPatterns.Extensions.Configuration.Tests.Net48`（net48 编译） | `IConfiguration` 桥接 |

```bash
dotnet test DesignPatterns.slnx
```

贡献流程见 [CONTRIBUTING.md](../CONTRIBUTING.md)。产品 backlog 见 [ROADMAP.md](ROADMAP.md)。

## 提交与分支

- 默认分支：`main`
- 功能分支：`feature/<short-description>`
- 修复分支：`fix/<short-description>`
- 提交信息：祈使句、**英语**，需说明 **why**（例：`Add RegisterStrategy source generator for strategy registry`）。语言规则以 [`../AGENTS.md`](../AGENTS.md) 为准。

CI：GitHub Actions [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)（`main` push/PR：restore、Release build、test；checkout [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) 并 build + run；pack 校验）。

NuGet 消费者 smoke test：[`eng/nuget-smoke/`](../eng/nuget-smoke/)（`MetaPackage.Consumer` net8.0、`MetaPackage.Consumer.Net48` net48；`CiPack` 使用本地 pack；对 nuget.org 已发包运行 `NuGetConsumerSmokePublished --consumer-feed Published`）。

## 相关文档

- [docs/README.md](README.md) — 本目录导航
- [PUBLISHING.md](PUBLISHING.md) — NuGet 发版流程
- [FactoryKeyConventions.md](FactoryKeyConventions.md) — Strategy / Factory key 命名约定
- [AGENTS.md](../AGENTS.md) — AI 助手上下文
- [CONTRIBUTING.md](../CONTRIBUTING.md) — 贡献与测试流程
- [ROADMAP.md](ROADMAP.md) — 功能 backlog
- [Decorator.md](Decorator.md) — Decorator 模式设计与 API
- [EventAggregator.md](EventAggregator.md) — Event Aggregator 模式设计与 API
- [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs) — 用户向文档站

## 待决事项

- [x] 开源许可证（[MIT](../LICENSE)）
- [x] 预览 NuGet 发布流程（`Skymly.DesignPatterns`，tag → Actions；见 [PUBLISHING.md](PUBLISHING.md)）
- [x] NuGet 元包多目标打包（`netstandard2.0` + `net8.0` 运行时；见 [`../AGENTS.md`](../AGENTS.md) 打包策略）
- [ ] 稳定版 SemVer 与 GitHub Release（API 稳定前 README 标明早期阶段；见 [ROADMAP.md](ROADMAP.md)）
- [ ] `DesignPatterns.Extensions.DependencyInjection` 独立 NuGet 发布策略
