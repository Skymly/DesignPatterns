# AGENTS.md

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 项目状态

| 项 | 说明 |
|----|------|
| **类型** | 个人项目（Skymly 工作区） |
| **远端** | https://github.com/Skymly/DesignPatterns（**公开**）；文件夹名 `DesignPatterns` = 仓库名 |
| **许可证** | [MIT](LICENSE) |
| **阶段** | **早期预览**：公共 API、生成器产出与 `DP###` 诊断**尚未稳定**（见 [README.md](README.md)） |
| **NuGet** | 元包 `DesignPatterns` 可本地 `dotnet pack`；nuget.org 正式发布与 SemVer 待 API 稳定后由维护者决定 |

## 项目是什么

**DesignPatterns** 是一个 .NET 设计模式工具库：

- **运行时**：轻量、可组合的 primitives（责任链、策略、工厂注册表、Singleton 特性等），组合优于继承。
- **编译期**：`DesignPatterns.SourceGenerators` 源生成器；`DesignPatterns.Analyzers` 诊断；`DesignPatterns.CodeFixes` CodeFix。

不要把它做成「23 种模式的厚重框架」或 MediatR/Polly 的替代品。

---

## 仓库结构

```
DesignPatterns.slnx
├── DesignPatterns/                              # 运行时核心（netstandard2.0 + net8.0）
├── DesignPatterns.Diagnostics/                  # DiagnosticIds 常量（DP001–DP023）
├── DesignPatterns.SourceGenerators/             # 增量源生成器
├── DesignPatterns.Analyzers/                    # DP006、DP023 Analyzer
├── DesignPatterns.CodeFixes/                    # CodeFixProvider
├── DesignPatterns.Extensions.DependencyInjection/  # MSDI 扩展 + DI 生成器 targets
├── DesignPatterns.Package/                      # NuGet 元包（PackageId=DesignPatterns）
├── tests/                                       # 单元 / 生成器 Verify / Analyzer / DI
├── samples/                                     # 按模式划分的示例
├── docs/                                        # DEVELOPMENT、ROADMAP、模式文档
├── .github/                                     # Issue/PR 模板、CI
└── AGENTS.md
```

规划但**尚未实现**：CompletionProvider。

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
| **Package** | `DesignPatterns.Package/` |
| **Samples** | `samples/` |
| **Docs** | `docs/`、根 `README.md`、`CONTRIBUTING.md` |
| **Repository** | `.github/`、`DesignPatterns.slnx`、`global.json`、`Directory.Build.props` |

模式域（Behavioral / Creational / Structural）变更仍须落在上表某一模块内；跨模式且跨模块时拆多个 Issue → PR。

---

## 常用命令

```powershell
dotnet restore DesignPatterns.slnx
dotnet build DesignPatterns.slnx -c Release
dotnet test DesignPatterns.slnx -c Release
```

本地打包：

```powershell
dotnet pack DesignPatterns.Package/DesignPatterns.Package.csproj -c Release -o artifacts/packages
```

启用生成器 DI 路径：引用 `DesignPatterns.Extensions.DependencyInjection`（自动 Import `build/DesignPatterns.Extensions.DependencyInjection.targets`）。

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

模式文档：[docs/Strategy.md](docs/Strategy.md)、[docs/ChainOfResponsibility.md](docs/ChainOfResponsibility.md)、[docs/Composite.md](docs/Composite.md)、[docs/FactoryRegistry.md](docs/FactoryRegistry.md)、[docs/Decorator.md](docs/Decorator.md)、[docs/EventAggregator.md](docs/EventAggregator.md)。

---

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

常量：[`DesignPatterns.Diagnostics/DiagnosticIds.cs`](DesignPatterns.Diagnostics/DiagnosticIds.cs)。规则表：[`DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md`](DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md)。

---

## 构建与 CI

| Workflow | 触发 | 作用 |
|----------|------|------|
| [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | PR / push `main` / `workflow_dispatch` | Restore → Build → Test → 全部 `samples/` → **Pack** 元包 artifact（**不** 发布 NuGet） |

与 CI 一致：提交前本地执行 `dotnet build` + `dotnet test`（Release 配置）。

---

## 版本、Tag 与 NuGet（代理与维护者）

遵循工作区根 [`../AGENTS.md`](../AGENTS.md) 的 Tag / 版本号约定，本仓库补充：

| 场景 | 代理行为 |
|------|----------|
| 用户**未**提及新版本号 / tag | **不得**改 `PackageVersion`、**不得** `git tag` / `gh release` / `dotnet nuget push` |
| API **未**宣布稳定 | README 保持早期阶段说明；破坏性变更可在无 major 版本策略下合并 |
| 用户明确要求发版 | 可准备命令草稿，标注**待批准**；预览版与稳定版策略日后与 Observables 对齐时可补 `release.yml` |

**CI 不会在 PR 或 push `main` 时发布 NuGet**；仅验证并上传 pack artifact。

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
| — | CodeFix + DP006/DP023 | 已完成 |

功能 backlog：[docs/ROADMAP.md](docs/ROADMAP.md)。

---

## 测试要求

| 层级 | 项目 |
|------|------|
| 单元 / 集成 | `tests/DesignPatterns.Tests` |
| 生成器快照 | `tests/DesignPatterns.SourceGenerators.Tests` |
| Analyzer / CodeFix | `tests/DesignPatterns.Analyzers.Tests` |
| DI 扩展 | `tests/DesignPatterns.Extensions.DependencyInjection.Tests` |

详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

## Git / Issue / PR / Commit

- 遵循工作区根 [`../AGENTS.md`](../AGENTS.md)：Issue / PR / Commit 默认**英语**；与用户对话默认**简体中文**。
- Issue 模板：[`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/)（Bug / Feature / Generator）。
- PR 模板：[`.github/pull_request_template.md`](.github/pull_request_template.md)。
- **禁止**在 Commit / PR 中提及 AI / Agent 工具。

---

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档默认英语。

## 待用户决策项（非阻塞）

- NuGet 公开发布与 SemVer（API 稳定后）
- 元包是否同时打包 `net8.0` 运行时 TFM
- `release.yml`（tag 触发 Publish）是否在发版流程确定后添加
