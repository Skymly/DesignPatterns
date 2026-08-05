# Design Doc: Fork–Join Work Graph

> **版本**：v0.2.4-preview1
> **关联 ADR**：—（执行波次借鉴 [ADR-006](../adr/ADR-006-composite-parallel-traversal.md) 同层并行思路，**不**扩展 Composite API）
> **关联 Issue**：[Spec #308](https://github.com/Skymly/DesignPatterns/issues/308)；落地 #309–#311；本 Design Doc [#312](https://github.com/Skymly/DesignPatterns/issues/312)

## 概述

**Fork–Join Work Graph**（API 前缀 `Work*`）提供**进程内异步 DAG**：步骤共享同一 `TContext`，边仅表示**就绪依赖**（非类型化 payload channel）。双路径装配：

- **手动**：`WorkGraphBuilder<TContext>` → `IWorkGraph<TContext>.RunAsync`
- **属性 + 生成器**：`[WorkGraph]` / `[WorkGraph<TContext>]` + `[WorkStep]` → `{Holder}WorkStepKeys` + `{Holder}WorkGraph.Create(resolver|dictionary)`

执行语义为拓扑**波次**：同波可并发；失败则 cancel 同波 in-flight peers 并 **fail-fast** 抛出。库不隔离 / 合并 context——同波对 `TContext` 的重叠无同步写入由调用方禁止。

## 设计目标

1. Holder 固定图名与 `TContext`；步骤类型独立可测
2. `[WorkStep(typeof(Holder), Id, DependsOn)]` 显式归属，允许多图共享同一 context 类型而不静默聚合
3. 边 = readiness only；输出经共享 context 突变，不经 `TIn`/`TOut` channel
4. 运行时 `Build` 与生成器共享合法性矩阵（环 / 未知依赖 / 重复 id / 自依赖 / 契约）
5. 多根合法；不可达步骤仅 Warning（DP091）
6. MVP：async-only、无 Dop / 追踪 / DI / sync `Execute`、双 TFM

## API 面

命名空间：`DesignPatterns.Behavioral`。

### 运行时

| 类型 | 职责 |
|------|------|
| `IWorkStep<TContext>` | `ValueTask ExecuteAsync(TContext, CancellationToken)` |
| `IWorkGraph<TContext>` | `ValueTask RunAsync(TContext, CancellationToken)` |
| `WorkGraphBuilder<TContext>` | `Add(id, step, params dependsOn)` → `Build()` |
| `InvalidWorkGraphException` | 空图 / 重复 id / 自依赖 / 未知依赖 / 环 |
| `WorkGraphAttribute` / `WorkGraphAttribute<TContext>` | 标记 holder（后者需 generic attributes，C# 11+ / net7+） |
| `WorkStepAttribute` | `Graph`、`Id`、`DependsOn`；`AllowMultiple = true` |

```csharp
public interface IWorkStep<TContext>
{
    ValueTask ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}

public interface IWorkGraph<TContext>
{
    ValueTask RunAsync(TContext context, CancellationToken cancellationToken = default);
}

public sealed class WorkGraphBuilder<TContext>
{
    public WorkGraphBuilder<TContext> Add(string id, IWorkStep<TContext> step, params string[] dependsOn);
    public IWorkGraph<TContext> Build(); // empty / illegal DAG → InvalidWorkGraphException
}
```

### 属性契约

- Holder：推荐 `static class`；`[WorkGraph(typeof(TContext))]` 或 `[WorkGraph<TContext>]`
- Step：实现 `IWorkStep<TContext>`；`[WorkStep(typeof(Holder), Id = "…", DependsOn = new[] { "…" })]`
- Id：图内唯一非空白字符串；`DependsOn` 引用同 holder 下已声明 id

### 生成器产出

`WorkGraphGenerator` 发出 `{Holder}WorkGraph.g.cs`（同 holder 命名空间）：

| 生成符号 | 说明 |
|----------|------|
| `{Holder}WorkStepKeys` | `public const string` 步 id（Strategy/Factory Keys 先例） |
| `{Holder}WorkGraph.Create(Func<string, IWorkStep<TContext>>)` | 填 `WorkGraphBuilder` 后 `Build()` |
| `{Holder}WorkGraph.Create(IReadOnlyDictionary<string, IWorkStep<TContext>>)` | 字典重载，委托到 resolver 路径 |

空 catalog 仍发出 `Create`，使运行时 `Build()` 抛 `InvalidWorkGraphException`（与手动空图一致）。无 MVP DI / Autofac 发射。

### 调用示例（示意）

```csharp
[WorkGraph<PrepContext>]
public static class RequestPrep { }

[WorkStep(typeof(RequestPrep), Id = "auth")]
sealed class AuthStep : IWorkStep<PrepContext> { /* … */ }

[WorkStep(typeof(RequestPrep), Id = "load-config")]
sealed class LoadConfigStep : IWorkStep<PrepContext> { /* … */ }

[WorkStep(typeof(RequestPrep), Id = "build-principal", DependsOn = new[] { "auth", "load-config" })]
sealed class BuildPrincipalStep : IWorkStep<PrepContext> { /* … */ }

[WorkStep(typeof(RequestPrep), Id = "authorize", DependsOn = new[] { "build-principal" })]
sealed class AuthorizeStep : IWorkStep<PrepContext> { /* … */ }

// 手动
var manual = new WorkGraphBuilder<PrepContext>()
    .Add(RequestPrepWorkStepKeys.Auth, new AuthStep())
    .Add(RequestPrepWorkStepKeys.LoadConfig, new LoadConfigStep())
    .Add(RequestPrepWorkStepKeys.BuildPrincipal, new BuildPrincipalStep(),
        RequestPrepWorkStepKeys.Auth, RequestPrepWorkStepKeys.LoadConfig)
    .Add(RequestPrepWorkStepKeys.Authorize, new AuthorizeStep(),
        RequestPrepWorkStepKeys.BuildPrincipal)
    .Build();

// 生成
var generated = RequestPrepWorkGraph.Create(id => id switch
{
    RequestPrepWorkStepKeys.Auth => new AuthStep(),
    RequestPrepWorkStepKeys.LoadConfig => new LoadConfigStep(),
    RequestPrepWorkStepKeys.BuildPrincipal => new BuildPrincipalStep(),
    RequestPrepWorkStepKeys.Authorize => new AuthorizeStep(),
    _ => throw new ArgumentOutOfRangeException(nameof(id)),
});

await generated.RunAsync(new PrepContext(), CancellationToken.None);
```

## 执行模型

| 规则 | 行为 |
|------|------|
| 拓扑波次 | Kahn 风格 indegree；indegree 0 组成一波 |
| 同波 | 可并发（`Task.WhenAll`）；单步波串行 `await` |
| Fail-fast | 第一步失败 → linked CTS cancel 同波 peers → 重抛失败（过滤取消噪声） |
| Context | 共享同一实例；无 isolate/merge；同波无同步写禁止 |
| 空图 | 非法 |
| 单节点 / 菱形 / 多根 | 合法 |

## 诊断

全部为**生成器**诊断（MVP 无 Analyzer / CodeFix）。常量见 `DiagnosticIds`；描述符见 `DesignPatternsDiagnosticDescriptors`。运行时 `Build` 对空图 / 环 / 重复 / 自依赖 / 未知依赖抛 `InvalidWorkGraphException`（不复用 DP 号）。

| ID | 严重性 | 触发条件 | 建议动作（摘要） |
|----|--------|----------|------------------|
| **DP087** | Error | `DependsOn` 成环 | 移除或改派环边 |
| **DP088** | Error | `DependsOn` 引用未声明 id | 注册该 id 的 `[WorkStep]` 或删除依赖 |
| **DP089** | Error | 同 holder 下 id 重复 | 重命名使 id 唯一 |
| **DP090** | Error | 步依赖自身（与 DP087 分开） | 从 `DependsOn` 去掉自身 |
| **DP091** | Warning | 从任一根（无 `DependsOn`）不可达 | 接边或删除孤立步；多根本身合法 |
| **DP092** | Error | `[WorkStep]` 未实现 holder 的 `IWorkStep<TContext>` | 实现契约或修正 holder / 特性 |

未注册 `IWorkStep` Analyzer：**非** MVP（Phase 2+），以免强制手动 builder 走属性路径。

## 不变量 / 兼容基线

- 双 TFM：`netstandard2.0` + `net8.0`；generic attribute 形态仅在支持平台编译
- Core **不**引用 MSDI（[ADR-004](../adr/ADR-004-core-does-not-reference-msdi.md)）
- 无 AppDomain 反射扫描注册
- Roslyn 4.8.0 增量生成器（`ForAttributeWithMetadataName`）
- 公开 API XML 文档；nullable enable；`TreatWarningsAsErrors`
- DP067–DP071 仍专属 [ADR-008](../adr/ADR-008-singleton-lifecycle-diagnostics.md)，不得改派

## 实现概览

### 运行时

- `WorkGraphBuilder` 校验后计算波次，冻结为内部 `WorkGraph<TContext>`
- `RunAsync` 逐波执行；多步波用 linked CTS + fail-fast

### 源生成器

`WorkGraphGenerator`（`IIncrementalGenerator`）：

1. 收集 `[WorkGraph]` / `[WorkGraph<T>]` holders 与 `[WorkStep]` 成员
2. 按 holder 聚合 catalog → 诊断 DP087–DP092
3. 成功则发出 Keys + `Create` facade（空 catalog 亦发 `Create`）

### 测试主缝

1. `DesignPatterns.Tests` — `Build` 校验与 `RunAsync` 波次 / fail-fast
2. `DesignPatterns.SourceGenerators.Tests` — Verify Keys/facade + 诊断快照

## 设计权衡

### 为何 readiness 边而非 typed channel

与观望域 **Channel Pipeline** 划界：本域边只表达「何时可跑」，数据经共享 `TContext`。避免把 BCL `Channel<T>` / TPL Dataflow 消息网拉进 Core。

### 为何 holder + 显式 `Graph` 归属

同 `TContext` 可有多图；若按 context 类型静默聚合会串图。显式 `typeof(Holder)` 与 Step Builder holder、Strategy Keys 先例一致。

### 为何自依赖单独 DP090

自依赖是局部、可立即修复的错误；与多节点环（DP087）分开便于消息与测试断言。

### 为何不可达仅 Warning

多根（如 `Auth` ∥ `LoadConfig`）合法；真正孤立步应可见但不阻塞编译成功路径。

### 为何 MVP 无 DI

属性 catalog 的步骤实例化留给 `Create(resolver)`；容器生命周期与 captive 分析留 Phase 2，避免 Core 碰 MSDI。

## 与生态的边界

### vs Channel Pipeline（观望）

| | Work Graph | Channel Pipeline（未准入） |
|---|---|---|
| 边 | readiness id | 类型化 `TIn`/`TOut` / `Channel<T>` |
| 数据 | 共享 `TContext` | 阶段间消息 |

### vs Composite parallel（ADR-006）

| | Work Graph | Composite `TraverseParallel*` |
|---|---|---|
| 结构 | 多前驱 DAG | 树 / 森林 |
| 并行单位 | 拓扑同波 | 同层 BFS / 子节点 |
| API | **不**扩展 `CompositeTraverser` | 树遍历专用 |

### vs Step Builder

| | Work Graph | Step Builder |
|---|---|---|
| 证明对象 | 执行就绪 DAG | 构造步完备（type-state） |
| 时间 | `RunAsync` 运行时波次 | `Build()` 前编译期门闩 |

### vs Command Router

1:1 CLR 命令分发，不是 fork–join 图。

### vs TPL Dataflow

不是消息块网络 / actor mailbox；可消费 `Task`/`ValueTask`/`WhenAll`，但不包装 Dataflow。

## 已知局限（非目标 / Phase 2+）

- **无** MVP `MaxDegreeOfParallelism` / run options
- **无** MVP `RunAsync` 追踪 / observer
- **无** MVP sync `Execute`
- **无** MVP aggregate / continue-on-error
- **无** MVP MSDI / Autofac 注册
- **无** MVP 未注册 `IWorkStep` Analyzer
- **无** `TContext` isolate/merge 框架
- **无** 类型化 payload 边

Samples（request-prep）落在 sibling [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples)，不在本仓 Docs PR。

## 参考

- Spec：[Fork–Join Work Graph (#308)](https://github.com/Skymly/DesignPatterns/issues/308)
- 落地：#309 Runtime → #310 Diagnostics（DP087–DP092）→ #311 SourceGenerators → #312 Docs → Samples（sibling）
- Wayfinder：[#300](https://github.com/Skymly/DesignPatterns/issues/300) / 准入地图 [#244](https://github.com/Skymly/DesignPatterns/issues/244)
- [ADR-006](../adr/ADR-006-composite-parallel-traversal.md) — Composite 同层并行（对照，非依赖）
- [docs/ROADMAP.md](../ROADMAP.md) F3 Top-3
- [AGENTS.md](../../AGENTS.md) — 模式摘要与诊断表
- [StepBuilder.md](StepBuilder.md) — 构造完备 vs 执行 DAG
- [Composite.md](Composite.md) — 树并行遍历对照
- [CommandRouter.md](CommandRouter.md) — 1:1 分发对照
