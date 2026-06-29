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
| Composite | `MyContractCompositeKeys`、`MyContractCompositeCatalog.BuildRoot()` / `BuildForest()` |
| Decorator | `{Contract}DecoratorStack.Build(core)`、`{Contract}DecoratorOrder` |
| Factory | `{Contract}Keys`、`{Contract}Registry` |
| Event Aggregator | `{Event}EventHandlerRegistry` |
| State | `{StateEnum}TransitionTable`、partial `{Holder}` 便捷方法 |

新增生成器必须沿用此命名风格（详见 [Decorator.md](Decorator.md)）。诊断 ID 续接现有区段，下一个可用 ID 为 **DP056**（DP053–DP055 为 Factory async + pooling 签名/池化校验，DP050–DP052 为 Handler guard 签名校验，DP047–DP049 为 Strategy guard 签名校验，DP044–DP046 为 EventAggregator 源生成器 + Analyzer 诊断，DP042–DP043 为 Decorator DI + async 签名校验，DP040–DP041 为 Composite DI + visitor 覆盖校验，DP037–DP039 为 State entry/exit action 诊断，DP032–DP035 为 State guard 诊断，DP036 为 State 字面量边校验；ID 一经发布不复用，详见 [AGENTS.md](../AGENTS.md)）。

诊断 ID 预分配（F2+ 增强项，登记后不提前占用，实现时按序领取）：

| ID 范围 | 用途 | 归属 |
|---------|------|------|
| DP037–DP039 | State entry/exit action 签名校验 | SourceGenerators |
| DP040–DP041 | Composite DI + visitor 覆盖校验 | SourceGenerators / Analyzers |
| DP042–DP043 | Decorator DI + async 签名校验 | SourceGenerators |
| DP044–DP046 | EventAggregator 未注册 handler + 重复事件类型 + 契约不匹配 | Analyzers / Generators |
| DP047–DP049 | Strategy guard 签名校验（已发布） | SourceGenerators |
| DP050–DP052 | Handler guard 签名校验（已发布） | SourceGenerators |
| DP053–DP055 | Factory async 签名 + 池化校验（已发布） | SourceGenerators |
| DP056+ | 保留（长期探索项按需领取） | — |

---

## 主线：功能扩展

按优先级分阶段。`[ ]` 待办、`[~]` 进行中、`[x]` 已完成。每项落地仍须遵守 [`../AGENTS.md`](../AGENTS.md) 的单模块 PR 边界。

### F1 — IDE 体验与诊断增强（近期）

| 项 | 说明 | 状态 |
|----|------|------|
| CompletionProvider | 自定义 Roslyn CompletionProvider 经 NuGet 元包不可被 IDE 加载；需独立 VSIX/Rider 插件，暂不排期。成员补全已由 `public const string` 覆盖 | [ ] |
| 字面量键校验 | DP025：对生成器管理的 Strategy/Factory 注册表调用点校验常量字符串键，并提供最近键 CodeFix | [x] |
| 诊断信息增强 | 现有 `DP###` 增加更可操作的 message / help link；统一 `DiagnosticDescriptor` 文案风格 | [x] |
| CodeFix 补全 | 为尚无 CodeFix 的可机械修复诊断补 Provider；评估 Composite 结构类诊断是否可部分自动化（DP010–012 仍归「明确不做」） | [x] |
| 跨编译单元未注册检测 | 评估 DP006/DP023/DP024 在多项目/分部注册场景下的误报与可达性 | [x] |

### F2 — 模式能力增强（结构性）

| 项 | 说明 | 状态 |
|----|------|------|
| Composite 多根 / 森林 | 多根 catalog（`AssembleForest` / `BuildForest`）与森林遍历（`TraverseForest`），保持 `BuildRoot()` 向后兼容 | [x] |
| Decorator 排序 / 条件 | 条件装饰（`DecoratorStackBuilder` 谓词 `Add`）、`{Contract}DecoratorOrder` int 常量（DP016 重复 Order 校验保留） | [x] |
| Handler 增强 | 评估管道分支 / 短路可观测性（不引入完整中间件框架） | [x] |

### F2+ — 现有模式增强（技术探索，按推荐顺序）

基于全仓库评估，以下增强聚焦「编译期 + 运行时协同」探索价值，**允许与生态库能力重叠**。每项仍须遵守单模块 PR 边界。

#### 第一梯队：高探索价值 + 编译期协同强

| 项 | 说明 | 涉及诊断 | 状态 |
|----|------|----------|------|
| State entry/exit actions + 实例包装器 | `[Transition(..., OnEnter/OnExit)]` 副作用钩子；生成 `IStateMachine<TState,TTrigger>` 自动跟踪 `CurrentState` 并触发 action；异步 action（`ValueTask` + `CancellationToken`）；生成器校验 action 方法签名（复用 guard 校验基础设施） | DP037–DP039 | [x] |
| Composite DI 集成 + Visitor 生成 | 生成 `RegisterDi(IServiceCollection, ServiceLifetime)` + `BuildRoot(IServiceProvider)` 从容器解析节点；生成 `I{Contract}NodeVisitor` 接口 + `AcceptVisitor` 分发，编译期校验 visitor 覆盖所有节点类型 | DP040–DP041 | [x] |
| Decorator DI 集成 + Async 变体 | 生成 `RegisterDi` + `Build(IServiceProvider, core)`；新增 `IAsyncDecorator<T>` + `DecorateAsync(T, CancellationToken)`，生成器双模式输出 | DP042–DP043 | [x] |
| EventAggregator 源生成器 + 自动订阅 | 新增 `[RegisterEventHandler<TEvent>]` + `RegisterEventHandlerGenerator`；生成 `{Event}HandlerRegistry.SubscribeAll(IEventAggregator)`（静态路径，无参构造）与 `RegisterDi` + `SubscribeAll(IEventAggregator, IServiceProvider)`（DI 路径，支持构造注入）；Analyzer 检测未标注的 `IEventHandler<T>` 实现 | DP044–DP046 | [x] |

#### 第二梯队：补齐能力短板

| 项 | 说明 | 涉及诊断 | 状态 |
|----|------|----------|------|
| Strategy/Chain guard 谓词 | Strategy：`Register(key, strategy, Func<TKey,bool>? guard)` + `TryGetWithGuard`；Chain：`[HandlerOrder<TContext>(order, Guard=nameof(Method))]`，trace 增加 `Skipped` 状态；生成器复用 State guard 签名校验 | DP047–DP052 | [x] |
| Factory async + 池化 | `IAsyncFactoryRegistry<TKey,TProduct>` + `CreateAsync(key, ct)`；可选 `PoolSize` 参数生成 `ArrayPool<T>` 支持的池化 registry；生成器双模式 sync+async | DP053–DP055 | [x] |
| State Autofac 支持 | ~~`StateTransitionSyntaxFactory` 加 `EnableAutofac` 分支，与 MSDI 对称（~30 行，快速补齐）~~（已完成，PR #170） | — | [x] |
| 生成代码质量提升 | ~~`#nullable enable` + `[GeneratedCode]` + `WithTrackingName` 已完成；剩余：所有生成器输出加 `/// <summary>` XML 文档~~（全部完成，PR #176/#177） | — | [x] |

#### 第三梯队：可观测性增强

| 项 | 说明 | 涉及诊断 | 状态 |
|----|------|----------|------|
| Strategy 执行追踪 | ~~`ExecuteTracedAsync` + `IStrategyExecutionObserver`；编译期按需生成 observer 分发~~（已完成，PR #183） | — | [x] |
| Chain 异常观测 | ~~`HandlerPipelineTrace` 增加 `Exception` + `FailedHandlerIndex`；可选 `IHandlerExceptionObserver`~~（已完成，PR #179） | — | [x] |
| EventAggregator 发布追踪 | ~~`PublishTracedAsync` + `EventPublicationTrace`；handler 错误聚合模式（`StopOnError` / `ContinueOnError` / `AggregateErrors`）（已完成，PR #166）；发布追踪（PR #181）~~ | — | [x] |

### F3 — 候选新模式（逐个评审，默认不承诺）

受 [`../AGENTS.md`](../AGENTS.md)「组合优于继承、primitives 而非厚重基类体系」约束。**与生态既有库（MediatR / Polly / `Microsoft.Extensions.*` / Stateless 等）能力重叠不构成拒绝理由**——本库以技术探索为目的，发挥编译期 + 运行时协同的最大潜能；候选只须满足下列**准入标准**即可进入 F1/F2：

- 能以**轻量 primitive + 编译期胶水**表达，而非厚重基类体系；
- 在源生成 / 诊断 / DI 集成等编译期能力上具备**技术探索价值**（哪怕运行时能力与现有库重叠）；
- 有真实使用场景与至少一个 Sample 设想。

候选池（仅登记，未排期）：Observer / 轻量 pub-sub 扩展、Builder 生成器、ObjectPool（探索源生成池化策略）、Resilience primitive（探索编译期策略组合）、Command 路由（探索与 MediatR 的差异点）。**范围包含 GoF 但不局限于 GoF**：并发模式、反应式模式、函数式模式等候选同样欢迎。**未通过准入前不实现。**

长期探索候选（高潜力、高复杂度，需 RFC 评审）：

| 候选 | 说明 | 探索价值 |
|------|------|----------|
| ~~State 层级状态机~~ | ~~`[StateMachine(..., Hierarchical = true)]` 支持嵌套状态 + 通配转换，生成器展平为快表~~ — **已在 v3 实现**（v3.1 运行时 `IStateHierarchy`、v3.2 `[StateParent]` + 展平 + DP056–DP059、v3.3 LCA + action 链合成、v3.4 DI + 示例 + 文档） | ⭐⭐⭐ 与 Stateless 重叠但展示「编译期展平层级」技术 |
| Composite 并行遍历 + 懒加载 | `TraverseParallel(root, visitor, degreeOfParallelism)` + `[CompositePart(..., LazyChildren = true)]` + `AssembleAsync` | ⭐⭐ 大树场景实用；AOT 友好并行调度 |
| Composite 树 schema 校验 | 编译期校验 max depth / parent-child 类型兼容性 / 节点计数 | ⭐⭐ 结构错误编译期捕获 |
| Decorator 组合 / 嵌套 | `DecoratorStackBuilder.Compose(otherStack)`，Analyzer 校验栈间类型兼容 | ⭐⭐ 可复用装饰器组合 |
| MSDI keyed services（.NET 8+） | 生成 keyed registration 代码（`#if NET8_0_OR_GREATER`），与 Autofac keyed 对称 | ⭐⭐ 补齐 MSDI keyed 缺口 |
| DI 健康检查 + 生命周期校验 | 生成 `IHealthCheck` 校验注册表键可解析；Analyzer 警告无效 lifetime 组合（Singleton registry + Transient impl） | ⭐⭐ 生产场景刚需 |
| Singleton 生命周期诊断 | Analyzer 检测 singleton 捕获 scoped/transient 引用；async 初始化支持 | ⭐⭐ 防 DI 反模式 |

State 转换表 v1 已于 0.1.0-preview4 发布；v2（guard 委托、DI 集成、DP036 字面量边校验）已实现，见 [StateTransitionTable.md](StateTransitionTable.md) 与 [rfc/StateTransitionTable.md](rfc/StateTransitionTable.md)。EventAggregator 联动示例仍为候选。

---

## 支线：工程整改（技术债，次要、不阻塞功能）

以下为已识别的不合理工程做法。规范目标值写入 [`../AGENTS.md`](../AGENTS.md)，此处仅作 backlog 跟踪。

| 项 | 现状 | 目标 | 状态 |
|----|------|------|------|
| 元包多目标缺失 | `DesignPatterns.Package` 仅打包 `netstandard2.0` 运行时 DLL（`_IncludeRuntimeInPackage`），net8.0 消费者拿不到 `FrozenDictionary` 优化版本 | 元包随运行时一并打包 `net8.0` lib | [x] |
| Roslyn 版本基线 | `Microsoft.CodeAnalysis.CSharp` 5.3.0 抬高消费者 SDK 门槛 | 下调至 **4.8.0**（与 VS 2022 17.8+ / .NET 8 SDK 对齐）；Analyzers 3.3.4 | [x] |
| xunit 版本分叉 | `Directory.Packages.props` 按 `MSBuildProjectName` 条件分出 2.9.2 / 2.5.3 | 统一单一 xunit 版本 | [x] |
| 覆盖率 collector 缺失 | `SourceGenerators.Tests`、`Analyzers.Tests` 未引用 `coverlet.collector`，CI 报「找不到 XPlat Code Coverage」 | 全部测试项目接入同一 collector | [x] |
| 缺 CHANGELOG | 无变更记录 | 引入 `CHANGELOG.md`（Keep a Changelog 风格） | [x] |
| 缺发布工作流 | 无 `release.yml` | tag 触发 Publish（见 AGENTS.md 维护者发版） | [x] |
| 生成代码缺 nullable/文档/标记 | ~~生成器输出无 `#nullable enable`、无 `[GeneratedCode]`、无 `/// <summary>` XML 文档~~ 全部完成：`#nullable enable` + `[GeneratedCode]` + `WithTrackingName`（commit b9a650e）；`/// <summary>` XML 文档 + 顺序修正（PR #176/#177） | 所有生成器统一加 nullable 头 + `[GeneratedCode]` + XML 文档；增量管线加 `WithTrackingName` 优化缓存 | [x] |

---

## 暂缓支线：稳定化与发布

当前**不**推进；记录前置条件，待功能主线收敛后由维护者重启。

- **API 冻结**：公共类型 / 特性 / 生成产出 / `DP###` 命名稳定声明（先行条件）。
- **打包修复**：元包多目标（`lib/net8.0`）已完成；「DI 扩展打包归属」仍待确定。
- **NuGet 首发 / SemVer**：`Skymly.DesignPatterns` `0.2.0-preview3`（`Directory.Build.props` + `release.yml`）；稳定版待 API 冻结。
- ~~**release.yml**~~：tag 触发 Publish — 已完成。

开关条件：F1/F2/F2+ 主要项落地且无未决破坏性设计时，再评估进入本支线。

---

## 明确不做

- AppDomain 反射扫描注册。
- Composite DP010–012 结构错误的自动 CodeFix（需人工修改结构）。

> 注：原「23 种 GoF 全量框架化」「复刻 MediatR / Polly / `Microsoft.Extensions.ObjectPool` 的完整能力」已于技术探索方针调整后**移除**——重叠不再是拒绝理由，详见 [AGENTS.md](../AGENTS.md)「项目是什么」。仍保留「primitives 而非厚重基类体系」的工程底线。

---

## 已完成（归档）

实现细节见 [`DEVELOPMENT.md`](DEVELOPMENT.md) 与对应模式文档。

- M1 运行时：Chain、Strategy、Factory Registry + 单元测试。
- R1 生成器：`[RegisterStrategy]`（DP003/004/007）、`[HandlerOrder]`（DP005/008/009）、`[GenerateSingleton]`（DP001/002）。
- M2 模式：Composite（`[CompositePart]`，DP010–015）、Decorator（`[Decorator]`，DP016–019）、EventAggregator（`IEventAggregator` / `IEventHandler<T>`）。
- P3 生态：`[RegisterFactory]`（DP020–022）、`IReadOnlyRegistry<TKey,TValue>`（仅 `IStrategyRegistry` 继承）、net8.0 `FrozenDictionary` 优化、`DesignPatterns.Extensions.DependencyInjection`。
- DI 与生成器打通：`RegisterDi` + `Create(IServiceProvider)`、`ServiceProviderStrategyRegistry`、集成测试。
- Analyzer / CodeFix：DP006 未注册策略、DP023 未注册工厂、DP024 未注册 Handler、DP025 未知注册表键；无参构造 / 接口实现 / RegisterStrategy / RegisterFactory / ICompositeBuildable / `partial`（DP001）/ `IDecorator<T>`（DP018）/ 最近键替换 CodeFix；跨程序集注册收集（DP006/023/024/025 多项目场景）。
- F1 诊断增强：集中 `DesignPatternsDiagnosticDescriptors`、可操作 message 与 help link（`DP001`–`DP024`）。
- Handler `AllowMultiple`：单类多 `[HandlerOrder]` / `[HandlerOrder<TContext>]`。
- 工程化：Nuke `Ci` / `CiPack`、GitHub Actions、warnings-as-errors、Pack 消费者验证、元包 `lib/net8.0` 打包与校验、共享注册生成器 Helper、共享未注册契约 Analyzer 基类。
- `Composite.Sample` 手动 `CompositeTreeBuilder` 演示。
- F2 Composite 多根 / 森林：`AssembleForest`、`BuildForest()`、`TraverseForest` / `TraverseForestAsync`（见 [Composite.md](Composite.md)）。
- F2 Decorator 排序 / 条件：`DecoratorStackBuilder` 谓词 `Add`、`{Contract}DecoratorOrder` 常量（见 [Decorator.md](Decorator.md)）。
- F2 Strategy 异步路径：`StrategyRegistryExtensions.ExecuteAsync` / `TryExecuteAsync`、`IAsyncStrategy` 契约的生成器与 DI 测试覆盖（见 [Strategy.md](Strategy.md)）。
- F2 Handler 增强：`HandlerPipeline.InvokeTracedAsync`、`HandlerPipelineTrace` 短路逐步可观测（见 [ChainOfResponsibility.md](ChainOfResponsibility.md)）。
- State 转换表 v1：`ITransitionTable` / `TransitionTableBuilder`（M1）、`[StateMachine]` / `[Transition]` 生成器（M2，DP026–DP031）；见 [StateTransitionTable.md](StateTransitionTable.md)。
- State 转换表 v2：guard 委托运行时 + 生成器（PR #124/#125，DP032/DP034/DP035）、DI 集成（PR #126，`RegisterDi` 生成 + `AddTransitionTable` 扩展）、DP036 字面量边校验 Analyzer（PR #127）；见 [StateTransitionTable.md](StateTransitionTable.md)。
- F2+ Strategy/Chain guard 谓词：`TryGetWithGuard` + `[RegisterStrategy(Guard=)]`（DP047–DP049）、`[HandlerOrder(Guard=)]` + `Skipped` trace 状态（DP050–DP052）；见 [Strategy.md](Strategy.md)、[ChainOfResponsibility.md](ChainOfResponsibility.md)。
- F2+ State entry/exit actions + IStateMachine：`[Transition(OnEnter/OnExit)]` + `IStateMachine<TState,TTrigger>` 实例包装器（DP037–DP039）；见 [StateTransitionTable.md](StateTransitionTable.md)。
- 工程整改：ROADMAP DP050–DP052 ID 冲突修复（PR #154）；StateTransitionTable docs entry/exit actions + IStateMachine 文档补全（PR #156）；Strategy docs guard 文档 + 接口签名修正（PR #158）。
- 工程整改：Nullable `[MaybeNullWhen]` 注解（Polyfill）+ FactoryRegistry `FrozenDictionary` 优化（PR #160）。
- 工程整改：`StateMachine.CurrentState` setter 改 internal + 线程安全 XML 文档（PR #162，**破坏性变更**）。
- F2+ TransitionTrace：`TransitionTrace<TState>` + `TryTransitionTracedAsync` 部分执行可观测性（PR #164）；见 [StateTransitionTable.md](StateTransitionTable.md)。
- F2+ EventAggregator 错误隔离：`EventPublishErrorHandling` 枚举（StopOnError/ContinueOnError/AggregateErrors）+ `PublishAsync` 重载（PR #166）；见 [EventAggregator.md](EventAggregator.md)。
- F2+ State Autofac 集成：生成 `RegisterAutofac` 到 transition table + state machine 类；`AutofacStateTransitionExtensions` 手动注册（PR #170）。
- 工程整改：README 清理 + 徽章 + NuGet 元数据增强（PR #171/#172）。
- 发版 `0.2.0-preview1`：minor 版本升级，含 State v2 完整能力、Composite/Decorator/EventAggregator 生成器、guard 谓词、错误隔离、Autofac 集成（PR #173，tag `v0.2.0-preview1`）。
- F2+ 生成代码 XML 文档：所有生成器输出加 `/// <summary>` XML 文档（PR #176）；修正 XML doc 与 `[GeneratedCode]` 属性顺序（PR #177）。
- F2+ Chain 异常观测：`HandlerPipelineStepStatus.Failed` + `HandlerPipelineStep.Exception` + `HandlerPipelineTrace.FailedHandlerIndex`/`Exception` + `IHandlerExceptionObserver<TContext>`（PR #179）。
- F2+ EventAggregator 发布追踪：`PublishTracedAsync` + `EventPublicationTrace` + `EventPublicationStep` + `IEventPublicationObserver<TEvent>`，支持三种错误处理模式（PR #181）。
- F2+ Strategy 执行追踪：`ExecuteTracedAsync` + `StrategyExecutionTrace<TOutput>` + `StrategyExecutionStepStatus` + `IStrategyExecutionObserver<TInput,TOutput>`，含 KeyNotFound/GuardRejected/Failed 状态 + Stopwatch 计时（PR #183）。
- 发版 `0.2.0-preview2`：含生成代码 XML 文档、Chain 异常观测、EventAggregator 发布追踪、Strategy 执行追踪（PR #185，tag `v0.2.0-preview2`）。
- F2+ Factory async + pooling 运行时：`IAsyncFactoryRegistry<TKey,TProduct>` + `CreateAsync(key, ct)` + `IPooledFactoryRegistry<TKey,TProduct>` + `ArrayPool<T>` 池化（PR #187）。
- F2+ Factory async + pooling 生成器：`[RegisterFactory]` 双模式 sync+async 生成 + `PoolSize` 参数 + DP053（async 签名不匹配）/ DP054（池大小无效）/ DP055（池大小过大警告）（PR #189）。
- F2+ Factory async/pooled DI + Autofac 集成：生成器输出 `RegisterDi` / `RegisterAutofac` for async/pooled registries（PR #191）；MSDI 扩展方法 `AddAsyncFactoryRegistry` / `AddPooledFactoryRegistry`（PR #193）；DI + Autofac 集成测试（PR #195）。
- Fix Factory `RegisterDi` 默认 `implementationLifetime` 从 `Singleton` 改为 `Transient`（PR #196，**破坏性变更**，匹配工厂语义）。
