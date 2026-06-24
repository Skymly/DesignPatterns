# AGENTS.md

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 项目状态

| 项 | 说明 |
|----|------|
| **类型** | 个人项目（Skymly 工作区） |
| **远端** | https://github.com/Skymly/DesignPatterns（**公开**）；文件夹名 `DesignPatterns` = 仓库名 |
| **许可证** | [MIT](LICENSE) |
| **阶段** | **早期预览**：公共 API、生成器产出与 `DP###` 诊断**尚未稳定**（见 [README.md](README.md)） |
| **NuGet** | 元包 **`Skymly.DesignPatterns`**；当前 **`0.1.0-preview7`**；`release.yml` + Nuke `Publish` → nuget.org + GitHub Packages |
| **Sibling 仓库** | [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples)、[DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs) — 工作区路径 `Skymly/DesignPatterns/DesignPatterns.Samples/`、`Skymly/DesignPatterns/DesignPatterns.Docs/` |

## 项目是什么

**DesignPatterns** 是一个以**技术探索**为目的的 .NET 设计模式工具库：发挥当前库的最大技术潜能，**即使与现有其他项目（MediatR / Polly / `Microsoft.Extensions.*` / Stateless 等）能力重叠也无所谓**——重叠不是拒绝实现的理由，能否在编译期胶水 + 运行时 primitives 的组合上做出有技术价值的探索才是判断标准。

- **运行时**：轻量、可组合的 primitives（责任链、策略、工厂注册表、Singleton 特性等），组合优于继承。
- **编译期**：`DesignPatterns.SourceGenerators` 源生成器；`DesignPatterns.Analyzers` 诊断；`DesignPatterns.CodeFixes` CodeFix。
- **语言**：**C# 优先**；后续再考虑 .NET 生态其他语言（F# / VB 等待评估）。
- **模式范围**：**包含 GoF 但不局限于 GoF**——并发模式、反应式模式、函数式模式、分布式模式等只要能用「primitive + 编译期胶水」表达且具备技术探索价值，均可纳入。

仍须遵守的硬约束（与探索方针并存，非自我设限而是工程底线）：primitives 而非厚重基类体系；Core 不引用 MSDI；异步一等；显式失败优先 `TryResolve` 与明确异常。

---

## 仓库结构

```
DesignPatterns.slnx
├── DesignPatterns/                              # 运行时核心（netstandard2.0 + net8.0）
├── DesignPatterns.Diagnostics/                  # DiagnosticIds 常量（DP001–DP040）
├── DesignPatterns.SourceGenerators/             # 增量源生成器
├── DesignPatterns.Analyzers/                    # DP006、DP023、DP024、DP025、DP033、DP036 Analyzer
├── DesignPatterns.CodeFixes/                    # CodeFixProvider
├── DesignPatterns.Extensions.DependencyInjection/  # MSDI 扩展 + DI 生成器 targets
├── DesignPatterns.Extensions.Autofac/              # Autofac 扩展 + Autofac 生成器 targets
├── DesignPatterns.Package/                      # NuGet 元包（PackageId=Skymly.DesignPatterns）
├── tests/                                       # 单元 / 生成器 Verify / Analyzer / DI
├── docs/                                        # DEVELOPMENT、ROADMAP、模式文档
├── .github/                                     # Issue/PR 模板、CI
└── AGENTS.md
```

**不**经 NuGet 元包实现：自定义 Roslyn `CompletionProvider`（IDE 宿主不加载项目引用的 Provider；成员补全已由 `*Keys` 的 `public const string` 与 DP025 字面量键校验覆盖）。独立 VSIX/Rider 插件暂不排期（见 [ROADMAP](docs/ROADMAP.md) F1）。

---

## 跨模块 PR / Issue 边界

与 [`DesignPatterns.slnx`](DesignPatterns.slnx) 一致；**每个模块单独 Issue + PR**（勿在同一 PR 混合多个模块）：

| 模块 | 范围 |
|------|------|
| **Runtime** | `DesignPatterns/` |
| **Diagnostics** | `DesignPatterns.Diagnostics/` |
| **SourceGenerators** | `DesignPatterns.SourceGenerators/` |
| **Analyzers** | `DesignPatterns.Analyzers/` + `DesignPatterns.CodeFixes/` |
| **DependencyInjection** | `DesignPatterns.Extensions.DependencyInjection/`、`build/*.targets` |
| **Autofac** | `DesignPatterns.Extensions.Autofac/`、`build/*.targets` |
| **Package** | `DesignPatterns.Package/` |
| **Docs** | 本仓 `docs/`（维护者/中文设计笔记）；用户文档站 [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs)（VitePress，分 PR） |
| **Repository (root README)** | 根 `README.md`、`CONTRIBUTING.md` |
| **Repository** | `.github/`、`DesignPatterns.slnx`、`global.json`、`Directory.Build.props`、`build/` |

模式域（Behavioral / Creational / Structural）变更仍须落在上表某一模块内；跨模式且跨模块时拆多个 Issue → PR。

---

## 常用命令

与 CI 一致（Nuke）：

```powershell
# 编译 + 测试（默认本地 Debug；CI 用 Release）
./build.ps1 --target Ci --configuration Release

# 打包 + 校验 nupkg → artifacts/package/
./build.ps1 --target CiPack --configuration Release
```

等价：

```powershell
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
dotnet run --project build/_build.csproj -- --target CiPack --configuration Release
```

传统 dotnet（仍可用，非 CI 权威路径）：

```powershell
dotnet build DesignPatterns.slnx -c Release
dotnet test DesignPatterns.slnx -c Release
```

启用生成器 DI 路径：引用 `DesignPatterns.Extensions.DependencyInjection`（自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`）。

启用生成器 Autofac 路径：引用 `DesignPatterns.Extensions.Autofac`（自动 Import `build/DesignPatterns.Extensions.Autofac.targets`）。

---

## 已实现的模式（摘要）

| 模式 | 特性 / API | 生成器 |
|------|------------|--------|
| Singleton | `[GenerateSingleton]` | `GenerateSingletonGenerator` |
| Factory Registry | `IFactoryRegistry`、`[RegisterFactory]` | `RegisterFactoryGenerator` |
| Strategy | `[RegisterStrategy]` | `RegisterStrategyGenerator` |
| Chain | `IHandler<T>`、`HandlerPipeline` | `HandlerOrderGenerator` |
| Composite | `[CompositePart]` | `CompositePartGenerator` |
| Decorator | `[Decorator]` | `DecoratorGenerator` |
| Event Aggregator | `IEventAggregator` | — |
| State（M1–M2） | `ITransitionTable`、`[StateMachine]`、`[Transition]` | `StateTransitionGenerator` |

模式文档：[docs/Strategy.md](docs/Strategy.md)、[docs/ChainOfResponsibility.md](docs/ChainOfResponsibility.md)、[docs/Composite.md](docs/Composite.md)、[docs/FactoryRegistry.md](docs/FactoryRegistry.md)、[docs/Decorator.md](docs/Decorator.md)、[docs/EventAggregator.md](docs/EventAggregator.md)、[docs/FactoryKeyConventions.md](docs/FactoryKeyConventions.md)、[docs/StateTransitionTable.md](docs/StateTransitionTable.md)、[docs/rfc/StateTransitionTable.md](docs/rfc/StateTransitionTable.md)。

---

## 编译期诊断 ID

| ID | 说明 |
|----|------|
| DP001–DP002 | GenerateSingleton |
| DP003–DP004、DP007 | RegisterStrategy（生成器） |
| DP005、DP008–DP009 | HandlerOrder（生成器） |
| DP006 | 未注册策略（Analyzer + CodeFix） |
| DP023 | 未注册工厂（Analyzer + CodeFix） |
| DP024 | 未注册 Handler（Analyzer + CodeFix） |
| DP025 | 未知注册表键字面量（Analyzer + CodeFix；Strategy/Factory `Get`/`TryGet`/`Create`/`TryCreate`） |
| DP033 | 跨程序集重复 strategy key（Analyzer；多供应商宿主引用冲突） |
| DP026–DP031 | State 转换表（生成器；重复边、非法 enum、holder、孤立态 Info） |
| DP032、DP034–DP035 | State 转换 guard（生成器；guard 方法未找到、非 static、签名错误） |
| DP036 | State 转换字面量边校验（Analyzer；`TryTransition` 字面量 (state, trigger) 对未声明） |
| DP037–DP039 | State 转换 entry/exit action（生成器；action 方法未找到、非 static、签名错误） |
| DP040 | Composite DI 节点未注册（生成器；BuildRoot(IServiceProvider) 时节点未注册到容器） |
| DP010–DP015 | CompositePart（生成器） |
| DP016–DP019 | Decorator（生成器） |
| DP020–DP022 | RegisterFactory（生成器） |

常量：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](DesignPatterns.Diagnostics/DiagnosticIds.cs)。规则表：[`DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md`](DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md)。

诊断 ID 规范（**本表为唯一登记源**，其他文档不得另立分类）：

- 下一个可用 ID：**DP041**；ID 一经发布不复用、不改语义。
- 新增 / 修改诊断必须同步 [`DiagnosticIds.cs`](DesignPatterns.Diagnostics/DiagnosticIds.cs)、[`DesignPatternsDiagnosticDescriptors.cs`](DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs)（经 Compile Link 编入 SourceGenerators / Analyzers）与 [`AnalyzerReleases.Unshipped.md`](DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md)。
- 归属：DP006 / DP023 / DP024 / DP025 / DP033 / DP036 属 **Analyzer**；其余属**生成器**。
- 文案：`messageFormat` 须含可操作建议；`description` 供 IDE 悬停；`helpLinkUri` 指向 [`DesignPatterns.Docs` diagnostics 页](https://skymly.github.io/DesignPatterns.Docs/diagnostics)（`#dp###` 片段，见 [`DiagnosticHelpLinks.cs`](DesignPatterns.Diagnostics/DiagnosticHelpLinks.cs)）。
- DP038（action 方法非 static）因 C# 编译器 CS0708 先于生成器拒绝 static 类中的实例成员，无法通过生成器测试触发；诊断保留供完整性。

---

## 编码标准

| 约定 | 要求 |
|------|------|
| Nullable | 全仓 `enable`（[`Directory.Build.props`](Directory.Build.props)） |
| 警告 | `TreatWarningsAsErrors=true`；提交前必须**零警告** |
| 命名空间 | file-scoped（`.editorconfig` `file_scoped:warning`） |
| 公共 API | 必带 XML 文档注释（`GenerateDocumentationFile`） |
| 命名 | 接口 `I` 前缀、异步方法 `Async` 后缀（遵循 [.NET 设计指南](https://learn.microsoft.com/dotnet/standard/design-guidelines/)） |
| 子命名空间 | 按 GoF 分 `DesignPatterns.Behavioral` / `Creational` / `Structural` |
| 风格 | 缩进 / 换行 / using 排序以 [`.editorconfig`](.editorconfig) 为准（此处不复制具体规则） |

设计原则（运行时）：primitives 而非框架；Core 不引用 MSDI（DI 归 `DesignPatterns.Extensions.DependencyInjection`）；异步一等（`CancellationToken` / `ValueTask`）；显式失败优先 `TryResolve` 与明确异常，避免静默 `null`。

---

## 兼容基线（TFM 与 Roslyn）

| 项 | 基线 |
|----|------|
| 运行时 TFM | `netstandard2.0` + `net8.0`（两者均须可用并随包分发） |
| Roslyn 组件 | **4.8.0**（`Microsoft.CodeAnalysis.CSharp` / Workspaces；Analyzers 3.3.4）；目标广泛兼容 VS 2022 17.8+ / .NET 8 SDK，**不**绑定最新 major |
| 分析器程序集 | `IsRoslynComponent`、`EnforceExtendedAnalyzerRules`、`IncludeBuildOutput=false` |
| 生成器实现 | `IIncrementalGenerator` + `ForAttributeWithMetadataName` |

---

## 打包策略

- 元包 **`Skymly.DesignPatterns`**（[`DesignPatterns.Package`](DesignPatterns.Package/DesignPatterns.Package.csproj)）必须打包运行时**所有 TFM** 的 `lib`（`netstandard2.0` **与** `net8.0`），不得只携带 `netstandard2.0`——否则 net8.0 消费者拿不到 `FrozenDictionary` 优化版本。
- 生成器 / Analyzer / CodeFix 打到 `analyzers/dotnet/cs`。
- DI 扩展 `DesignPatterns.Extensions.DependencyInjection` 的打包 / 独立发布归属待发版流程确定；当前元包**不含**。
- 任何打包结构变更须通过 `CiPack` 的 `PackVerify` / `PackConsumerVerify`。

---

## 构建与 CI

| Nuke 目标 | 说明 |
|-----------|------|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest` |
| **CiPack** | `Ci` 链 + `Pack` + `PackVerify` + `PackConsumerVerify` → `artifacts/package/*.nupkg` |
| **Publish** | `Test` → `Pack` → `PackVerify` → push 到 nuget.org（`NUGET_API_KEY`）与 GitHub Packages（`GITHUB_TOKEN`） |
| **NuGetConsumerSmoke** | `PackVerify` 后构建并运行 [`eng/nuget-smoke/MetaPackage.Consumer/`](../eng/nuget-smoke/MetaPackage.Consumer/)（默认本地 pack feed） |
| **NuGetConsumerSmokePublished** | 同上，但从 nuget.org 还原（`--consumer-feed Published`） |

| Workflow | 触发 | 作用 |
|----------|------|------|
| [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | PR / push `main` / `workflow_dispatch` | **Ci** + sibling **Samples** + **CiPack**（**不** 发布 NuGet） |
| [`.github/workflows/release.yml`](.github/workflows/release.yml) | push tag `v*` / `workflow_dispatch` | **Publish**（须 Secrets + 维护者 actor；**不** 创建 GitHub Release） |

提交前：本地 `./build.ps1 --target Ci --configuration Release`（或 `CiPack` 若改打包）。

---

## 版本、Tag 与 NuGet（代理与维护者）

本仓库 Tag / 版本号约定如下：

| 场景 | 代理行为 |
|------|----------|
| 用户**未**提及新版本号 / tag | **不得**改 `PackageVersion` / `VersionPrefix`、**不得** `git tag` / `gh release` / `dotnet nuget push` |
| API **未**宣布稳定 | README 保持早期阶段说明；破坏性变更可在无 major 版本策略下合并 |
| 用户明确要求发版 | 对齐 `Directory.Build.props` 版本与 `CHANGELOG.md`；可准备 tag / push 命令草稿，标注**待批准** |

**CI 不会在 PR 或 push `main` 时发布 NuGet**；仅验证并上传 pack artifact。

### 预览版 vs 稳定版

| 版本类型 | Git tag（`v*`） | NuGet（nuget.org + GitHub Packages） | GitHub Release |
|----------|-----------------|--------------------------------------|----------------|
| **预览**（如 `0.1.0-preview3`） | **要** | **要**（nuget.org + GitHub Packages） | **不要** |
| **稳定**（无 `-preview` 等预发布后缀） | **要** | **要** | **要**（维护者批准） |

### 维护者发版（tag 触发）

1. 在 `main` 上确认 [`Directory.Build.props`](Directory.Build.props) 中 **`PackageVersion` 与 tag 一致**（tag 为 `v` + 版本号，如 `v0.1.0-preview3`）。
2. 配置仓库 Secrets：`NUGET_API_KEY`；`GITHUB_TOKEN`（或 PAT，`packages:write`，用于 GitHub Packages）。
3. 推送 **annotated tag**：

```powershell
git tag -a v0.1.0-preview3 -m "0.1.0-preview3"
git push origin v0.1.0-preview3
```

4. [`release.yml`](.github/workflows/release.yml) 在 **`push` `v*` tag** 时运行 Nuke **`Publish`**（`Test` → `Pack` → `PackVerify` → nuget.org + GitHub Packages）。仅允许维护者账号（workflow 内 `github.actor` 校验）。**不**创建 GitHub Release。
5. 紧急重发可用 **workflow_dispatch** 并手动填写 `version`（仍受 actor 限制）。

版本与变更记录：

- 版本号：`VersionPrefix` + `VersionSuffix`（当前 `0.1.0-preview3`），单一真相源为 [`Directory.Build.props`](Directory.Build.props)；发版时可通过环境变量 `VERSION` 覆盖。
- 变更记录：[`CHANGELOG.md`](CHANGELOG.md)（Keep a Changelog 风格）。

---

## 当前里程碑

| 阶段 | 内容 | 状态 |
|------|------|------|
| M0 | 文档与仓库骨架 | 已完成 |
| M1 | Chain、Strategy、Factory Registry 运行时 + 单元测试 | 已完成 |
| R1 | `[RegisterStrategy]` + DP003/004/007 | 已完成 |
| R1+ | Singleton、HandlerOrder 生成器 | 已完成 |
| M2 | Composite / Decorator / EventAggregator | 已完成 |
| P3 | `[RegisterFactory]`、IReadOnlyRegistry、FrozenDictionary、DI 扩展包 | 已完成 |
| P3 | DI 与生成器打通（`RegisterDi` / `Create(IServiceProvider)`） | 已完成 |
| — | Nuke `Ci` / `CiPack`、GitHub Actions | 已完成 |
| — | Handler `AllowMultiple` | 已完成 |
| — | [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples)（含 `RegisterDi` 示例） | 已完成 |
| — | DP024 未注册 Handler Analyzer | 已完成 |
| — | CodeFix + DP006/DP023/DP024 | 已完成 |
| — | DP025 字面量键校验 Analyzer + CodeFix | 已完成 |
| — | F1 IDE 体验（诊断增强 / CodeFix / 跨程序集 / 字面量键校验） | 已完成 |
| — | State v2：guard 源生成 + DP032/DP034/DP035 | 已完成 |
| — | State v2：DI 集成（`RegisterDi` 生成 + `AddTransitionTable` 扩展） | 已完成 |
| — | State v2：DP036 字面量边校验 Analyzer | 已完成 |
| — | State v2：entry/exit actions + DP037-DP039 | 已完成 |

功能 backlog：[docs/ROADMAP.md](docs/ROADMAP.md)。

---

## 测试要求

| 层级 | 项目 |
|------|------|
| 单元 / 集成 | `tests/DesignPatterns.Tests` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests`（Verify） |
| Analyzer / CodeFix | `tests/DesignPatterns.Analyzers.Tests` |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests` |
| Autofac 扩展 | `tests/DesignPatterns.Extensions.Autofac.Tests` |

规范：

- **xunit 版本统一**：全部测试项目使用 [`Directory.Packages.props`](Directory.Packages.props) 中的单一 xunit / runner 版本（当前 2.9.2 / 2.8.2）。
- **覆盖率**：所有测试项目统一引用 `coverlet.collector` 并产出 `coverage.cobertura.xml`；覆盖率作为**参考门槛**（暂不硬阻塞）。
- **生成器快照**：改动生成代码后运行测试并审阅 `*.received.*`，确认后接受为 `*.verified.txt`。

详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

## Git / Issue / PR / Commit

- **语言（权威）**：Issue / PR / Commit **一律英语**；与用户对话默认**简体中文**。本条为权威表述，**覆盖** `docs/DEVELOPMENT.md`、`CONTRIBUTING.md` 中任何「中英文均可」的旧措辞。
- 分支：功能 `feature/<short-description>`、修复 `fix/<short-description>`；提交信息祈使句、说明 **why**。
- **每个 PR 只改一个模块**（边界见上文「跨模块 PR / Issue 边界」）。
- Issue 模板：[`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/)（Bug / Feature / Generator）。
- PR 模板：[`.github/pull_request_template.md`](.github/pull_request_template.md)。
- **禁止**在 Commit / PR 中提及 AI / Agent 工具。

---

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档默认英语。

## 待用户决策项（非阻塞）

- NuGet 公开发布时机与 SemVer 起始版本（API 冻结后）。
- DI 扩展是否独立发布为 NuGet 包，及其与元包的关系。

> 注：元包必须打包 `net8.0` 运行时 TFM 已定为标准（见「打包策略」），其落地作为工程整改项跟踪于 [docs/ROADMAP.md](docs/ROADMAP.md)，不再是开放问题。
