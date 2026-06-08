# 产品与技术路线图

本文档是 **DesignPatterns 的功能与工程 backlog**。它只记录「要做什么、为何做、是否做」，**不**定义规范——开发规范的权威源是 [`../AGENTS.md`](../AGENTS.md)，构建/测试操作见 [`DEVELOPMENT.md`](DEVELOPMENT.md)。

当前阶段以**功能扩展**为主线；稳定化与对外发布列为**暂缓支线**（见末尾）。已完成项的实现细节见 [`DEVELOPMENT.md`](DEVELOPMENT.md) 与各模式文档。

---

## 命名规则（生成器）

对契约接口 `IMyContract`（或类名）：

| 模式 | 生成类型示例 |
|------|----------------|
| Strategy | `MyContractKeys`、`MyContractRegistry` |
| Chain | `{Context}HandlerPipeline` |
| Composite | `MyContractCompositeKeys`、`MyContractCompositeCatalog.BuildRoot()` |
| Decorator | `{Contract}DecoratorStack.Build(core)`、`{Contract}DecoratorOrder` |
| Factory | `{Contract}Keys`、`{Contract}Registry` |

新增生成器必须沿用此命名风格（详见 [Decorator.md](Decorator.md)）。诊断 ID 续接现有区段，下一个可用 ID 为 **DP025**。

---

## 主线：功能扩展

按优先级分阶段。`[ ]` 待办、`[~]` 进行中、`[x]` 已完成。每项落地仍须遵守 [`../AGENTS.md`](../AGENTS.md) 的单模块 PR 边界。

### F1 — IDE 体验与诊断增强（近期）

| 项 | 说明 | 状态 |
|----|------|------|
| CompletionProvider | 为生成的 `*Keys` 常量 / 契约键提供 IDE 补全；优先级低于强类型常量与 CodeFix（见架构原则），需权衡维护成本 | [ ] |
| 诊断信息增强 | 现有 `DP###` 增加更可操作的 message / help link；统一 `DiagnosticDescriptor` 文案风格 | [ ] |
| CodeFix 补全 | 为尚无 CodeFix 的可机械修复诊断补 Provider；评估 Composite 结构类诊断是否可部分自动化（DP010–012 仍归「明确不做」） | [ ] |
| 跨编译单元未注册检测 | 评估 DP006/DP023/DP024 在多项目/分部注册场景下的误报与可达性 | [ ] |

### F2 — 模式能力增强（结构性）

| 项 | 说明 | 状态 |
|----|------|------|
| Composite 多根 / 森林 | v2 设计：支持多根 catalog 与森林遍历，保持 `BuildRoot()` 向后兼容 | [ ] |
| Decorator 排序 / 条件 | 条件装饰（按运行时谓词启用）、显式 `DecoratorOrder` 校验诊断 | [ ] |
| Strategy 异步路径 | 补全 `IAsyncStrategy` 的注册表 / 解析 / DI 路径与文档对齐 | [ ] |
| Handler 增强 | 评估管道分支 / 短路可观测性（不引入完整中间件框架） | [ ] |

### F3 — 候选新模式（逐个评审，默认不承诺）

受 [`../AGENTS.md`](../AGENTS.md)「组合优于继承、不做 GoF 全量框架」约束。任何候选须先满足**准入标准**再进入 F1/F2：

- 能以**轻量 primitive + 编译期胶水**表达，而非厚重基类体系；
- 不与生态既有库（MediatR / Polly / `Microsoft.Extensions.*`）重复造轮子；
- 有真实使用场景与至少一个 Sample 设想。

候选池（仅登记，未排期）：Observer / 轻量 pub-sub 扩展、Builder 生成器、State 转换表。**未通过准入前不实现。**

---

## 支线：工程整改（技术债，次要、不阻塞功能）

以下为已识别的不合理工程做法。规范目标值写入 [`../AGENTS.md`](../AGENTS.md)，此处仅作 backlog 跟踪。

| 项 | 现状 | 目标 | 状态 |
|----|------|------|------|
| 元包多目标缺失 | `DesignPatterns.Package` 仅打包 `netstandard2.0` 运行时 DLL（`_IncludeRuntimeInPackage`），net8.0 消费者拿不到 `FrozenDictionary` 优化版本 | 元包随运行时一并打包 `net8.0` lib | [ ] |
| Roslyn 版本基线 | `Microsoft.CodeAnalysis.CSharp` 版本偏高，抬高消费者 SDK 门槛 | 明确目标 Roslyn 基线并下调到广泛兼容版本 | [ ] |
| xunit 版本分叉 | `Directory.Packages.props` 按 `MSBuildProjectName` 条件分出 2.9.2 / 2.5.3 | 统一单一 xunit 版本 | [ ] |
| 覆盖率 collector 缺失 | `SourceGenerators.Tests`、`Analyzers.Tests` 未引用 `coverlet.collector`，CI 报「找不到 XPlat Code Coverage」 | 全部测试项目接入同一 collector | [ ] |
| 缺 CHANGELOG | 无变更记录 | 引入 `CHANGELOG.md`（Keep a Changelog 风格） | [ ] |
| 缺发布工作流 | 无 `release.yml` | 发版流程确定后补 tag 触发 Publish（见暂缓支线） | [ ] |

---

## 暂缓支线：稳定化与发布

当前**不**推进；记录前置条件，待功能主线收敛后由维护者重启。

- **API 冻结**：公共类型 / 特性 / 生成产出 / `DP###` 命名稳定声明（先行条件）。
- **打包修复**：完成「元包多目标」与「DI 扩展打包归属」整改。
- **NuGet 首发 / SemVer**：`VersionPrefix` + 预览后缀策略；nuget.org 首个公开版本。
- **release.yml**：tag 触发 Publish。

开关条件：F1/F2 主要项落地且无未决破坏性设计时，再评估进入本支线。

---

## 明确不做

- 23 种 GoF 全量框架化。
- AppDomain 反射扫描注册。
- Composite DP010–012 结构错误的自动 CodeFix（需人工修改结构）。
- 复刻 MediatR / Polly / `Microsoft.Extensions.ObjectPool` 的完整能力。

---

## 已完成（归档）

实现细节见 [`DEVELOPMENT.md`](DEVELOPMENT.md) 与对应模式文档。

- M1 运行时：Chain、Strategy、Factory Registry + 单元测试。
- R1 生成器：`[RegisterStrategy]`（DP003/004/007）、`[HandlerOrder]`（DP005/008/009）、`[GenerateSingleton]`（DP001/002）。
- M2 模式：Composite（`[CompositePart]`，DP010–015）、Decorator（`[Decorator]`，DP016–019）、EventAggregator（`IEventAggregator` / `IEventHandler<T>`）。
- P3 生态：`[RegisterFactory]`（DP020–022）、`IReadOnlyRegistry<TKey,TValue>`（仅 `IStrategyRegistry` 继承）、net8.0 `FrozenDictionary` 优化、`DesignPatterns.Extensions.DependencyInjection`。
- DI 与生成器打通：`RegisterDi` + `Create(IServiceProvider)`、`ServiceProviderStrategyRegistry`、集成测试。
- Analyzer / CodeFix：DP006 未注册策略、DP023 未注册工厂、DP024 未注册 Handler；无参构造 / 接口实现 / RegisterStrategy / RegisterFactory / ICompositeBuildable CodeFix。
- Handler `AllowMultiple`：单类多 `[HandlerOrder]` / `[HandlerOrder<TContext>]`。
- 工程化：Nuke `Ci` / `CiPack`、GitHub Actions、warnings-as-errors、Pack 消费者验证、共享注册生成器 Helper、共享未注册契约 Analyzer 基类。
- `Composite.Sample` 手动 `CompositeTreeBuilder` 演示。
