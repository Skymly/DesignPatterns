# Design Doc: Step Builder

> **版本**：v0.2.3-preview2
> **关联 ADR**：[ADR-010](../adr/ADR-010-step-builder-type-state-markers.md)
> **关联 Issue**：[#287](https://github.com/Skymly/DesignPatterns/issues/287)（Spec）、[#291](https://github.com/Skymly/DesignPatterns/issues/291)（本 Design Doc）；落地 #288–#290

## 概述

**Step Builder**（模式域名：**Builder**）提供声明式、分步构造产品的编译期胶水：schema 写在独立 **holder** 上，生成器发出带泛型 type-state 的 `{Holder}Builder`，使**缺必填步时 `Build()` 不可调用**。可选步、互斥组与偏序约束由诊断与生成 fluent 方法内的运行时检查覆盖。

文档用 **Step Builder** 称呼本域，以区别于库内既有的注册/装配 `*Builder`（`FactoryRegistryBuilder`、`CommandRouterBuilder`、`TransitionTableBuilder`、`DecoratorStackBuilder`、`CompositeTreeBuilder` 等）——那些组装的是注册表 / 管道 / 转换表，**不**证明产品构造步骤完备性。

## 设计目标

1. Schema 与产品 DTO 分离：`[GenerateBuilder]` 标在 holder，不污染领域类型
2. 必填完备性 = 编译期 type-state 证明（[ADR-010](../adr/ADR-010-step-builder-type-state-markers.md)）
3. 产品物化留在用户 `[BuilderAssemble]`：生成器只门闩 + 传参，不发明映射
4. 默认可任意顺序应用步骤；可选 `After` / `Before` 与 `MutexGroup`
5. 每步至多一次；必填步 ≤ 8；可选步不计入上限、不占 type 参数
6. MVP 同步、无 DI/Autofac、双 TFM（netstandard2.0 + net8.0）

## API 面

命名空间：`DesignPatterns.Creational`。

### 运行时（属性 + 标记类型）

本域 **无** 手写 fluent 运行时孪生；Core 仅提供特性与 phantom 标记。

| 类型 | 职责 |
|------|------|
| `GenerateBuilderAttribute` | 标记 schema holder |
| `BuilderStepAttribute` | 声明构造步骤（`Required` / `MutexGroup` / `After` / `Before`） |
| `BuilderAssembleAttribute` | 标记用户装配方法 |
| `BuilderStepState.NotSet` / `Set` | type-state phantom（不可实例化） |

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateBuilderAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BuilderStepAttribute : Attribute
{
    public bool Required { get; set; } = true;
    public string? MutexGroup { get; set; }
    public string? After { get; set; }
    public string? Before { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BuilderAssembleAttribute : Attribute { }

public static class BuilderStepState
{
    public sealed class NotSet { private NotSet() { } }
    public sealed class Set { private Set() { } }
}
```

### 持有者契约

- Holder：非泛型 `class`（可为 `static`），可访问性为 public / internal（含嵌套链），由 `[GenerateBuilder]` 标注
- 步骤方法：签名即 schema（方法体可空）；推荐 `WithX(...)` 命名，便于参数绑定剥离 `With` 前缀
- 装配：恰好一个 `[BuilderAssemble]`，返回非 void 产品类型；参数按名绑定到步骤（`WithUrl` → `url` / `Url` / `withUrl`，大小写不敏感回退）
- 产品类型 = assemble 返回类型（**不**在 `[GenerateBuilder]` 上重复声明）
- 可直接调用 assemble，绕过生成 fluent 类型

### 生成器产出

`GenerateBuilderGenerator` 发出 `{Holder}Builder.g.cs`（同 holder 命名空间），典型形状：

| 生成符号 | 说明 |
|----------|------|
| `{Holder}Builder`（静态入口） | `Create()` → 全 `NotSet` 的初始 builder |
| `{Holder}Builder<TStep…>` | 仅**必填**步对应类型参数 |
| `{Holder}BuilderState`（internal） | 捕获步值、`*Set` 标志、`AppliedOrder` |
| `{Holder}BuilderExtensions` | 步进扩展方法 + 门闩后的 `Build()` |

`Build()` 仅在全部必填类型参数为 `BuilderStepState.Set` 时作为扩展方法存在，内部调用 holder 的 assemble，未设置的可选步以 null 兼容实参传入。

示例（示意）：

```csharp
[GenerateBuilder]
public static class HttpRequestSchema
{
    [BuilderStep]
    public static void WithUrl(string url) { }

    [BuilderStep]
    public static void WithMethod(string method) { }

    [BuilderStep(Required = false)]
    public static void WithHeader(string header) { }

    [BuilderStep(Required = false, MutexGroup = "Auth")]
    public static void WithBearerToken(string token) { }

    [BuilderStep(Required = false, MutexGroup = "Auth")]
    public static void WithBasicAuth(string credentials) { }

    [BuilderAssemble]
    public static HttpRequest Assemble(
        string url, string method, string? header, string? bearerToken, string? basicAuth) =>
        new(url, method, header, bearerToken, basicAuth);
}

// 调用方：缺 WithMethod 时无 Build() 可解析重载 → 编译期失败
var request = HttpRequestSchemaBuilder.Create()
    .WithUrl("https://example.com")
    .WithMethod("GET")
    .WithBearerToken("…")
    .Build();
```

## 证明模型

| 约束 | 机制 |
|------|------|
| 必填步齐全 | 泛型 type-state；缺步则无 `Build()`（ADR-010） |
| 必填步数量 | ≤ 8；超额 **DP078**；可选步不计 |
| 步至多一次 | 必填：应用后该步扩展从 `NotSet` 接收端消失；可选：再应用抛 `InvalidOperationException` |
| 互斥组 | Schema：≥2 个**必填**同组 → **DP081**；应用时同组已设 → `InvalidOperationException` |
| `After` / `Before` | Schema：未知引用 **DP084**、约束成环 **DP082**；应用时违反偏序 → `InvalidOperationException` |
| 默认顺序 | 无 `After`/`Before` 时可任意交错必填/可选 |

> Spec 将可选/互斥/偏序归为「诊断优先」；落地后 schema 错误走 **DP078–DP086**，调用序列非法则在生成方法内抛异常（生成器无法在任意调用链上做主门闩，与 ADR-010「不以 Analyzer 作必填主门闩」一致）。

## 诊断

全部为**生成器**诊断（Error）。常量见 `DiagnosticIds`；描述符见 `DesignPatternsDiagnosticDescriptors`。

| ID | 触发条件 | 建议动作（摘要） |
|----|----------|------------------|
| **DP078** | 必填 `[BuilderStep]` > 8 | 将多余步标 `Required = false` 或拆分 holder |
| **DP079** | 有 `[GenerateBuilder]` 无 `[BuilderAssemble]` | 添加返回产品的装配方法 |
| **DP080** | assemble 参数名绑不到任何步骤 | 重命名参数或补步骤 |
| **DP081** | 同一 `MutexGroup` 内 ≥2 个必填步 | 至多保留一个必填，或移出组 / 改为可选 |
| **DP082** | `After`/`Before` 约束成环 | 修正偏序元数据 |
| **DP083** | 同名步骤重复（含 `With` 剥离后冲突） | 重命名使步名唯一 |
| **DP084** | `After`/`Before` 指向不存在的步骤 | 使用 `nameof` 兄弟步或删除约束 |
| **DP085** | holder 非法（非 class、泛型、不可访问等） | 改为可托管步骤的非泛型 class |
| **DP086** | assemble 契约非法（重复标注、void、不可访问实例装配等） | 修正签名或去掉重复标注 |

无本域 Analyzer / CodeFix（MVP）。

## 不变量 / 兼容基线

- 双 TFM：属性与生成代码在 netstandard2.0 与 net8.0 可用
- Core **不**引用 MSDI（[ADR-004](../adr/ADR-004-core-does-not-reference-msdi.md)）
- 无 AppDomain 反射扫描注册
- Roslyn 4.8.0 增量生成器（`ForAttributeWithMetadataName`）
- 公开 API XML 文档；nullable enable；`TreatWarningsAsErrors`

## 实现概览

### 运行时

仅属性与 `BuilderStepState` phantom 类型；无可变草稿 builder、无 Director 基类。

### 源生成器

`GenerateBuilderGenerator`（`IIncrementalGenerator`）：

1. 收集 `[GenerateBuilder]` holder → 校验可访问性 / 非泛型（失败 → DP085）
2. 收集 `[BuilderStep]` / `[BuilderAssemble]` → 缺装配 DP079；契约失败 DP086；重复步 DP083
3. 必填计数、互斥 schema、偏序图 → DP078 / DP081 / DP082 / DP084
4. assemble 参数绑定 → DP080
5. 成功则发出入口类型、泛型 builder、state、扩展方法（含运行时互斥/偏序/至多一次检查）

### 诊断检测逻辑

见上表；归属均为 Generator。测试主缝：`DesignPatterns.SourceGenerators.Tests` Verify（公开生成源 + 诊断快照）。

## 设计权衡

### 为何 holder 而非产品类型上挂特性

领域 DTO 保持干净；装配与步骤元数据集中在可单元测试的 schema 类型；绕过生成器时可直接测 assemble。

### 为何 type-state 而非 Analyzer 主门闩

缺必填时调用方直接无 `Build` 成员，失败更早、更本地；Analyzer 适合补充字面量/跨文件启发式，不能替代类型系统证明（ADR-010）。

### 为何可选步用 `T?` / null 而非 `Optional<T>`

避免强制全域包装类型；assemble 内可分支。与库「primitives」取向一致。

### 为何必填上限 8

泛型 arity 与可读性边界；超额应拆分 schema 或降级为可选（DP078）。

## 与生态的边界

### vs 注册 / 装配 `*Builder`（本库内）

| | Step Builder | 注册/装配 `*Builder` |
|---|---|---|
| 目的 | 证明**产品**多步构造完备 | 组装 registry / router / table / stack |
| 典型类型 | `{Holder}Builder`（生成） | `FactoryRegistryBuilder`、`CommandRouterBuilder`、`TransitionTableBuilder`、… |
| 完备性 | 必填步 type-state | 通常「注册完即 `Build`」，无产品步证明 |
| 生成器 | `[GenerateBuilder]` | 各域自有（或纯手写 builder） |

命名碰撞是有意的 fluent 习惯；选型看「要的是产品步骤证明，还是注册表装配」。

### vs Factory Registry

| | Step Builder | Factory Registry |
|---|---|---|
| 键 | 无（单次构造会话） | `TKey` → 产品 |
| 构造 | 多步累积 → 一次 assemble | 每次 `Create(key)` |
| 编译期 | 步完备 type-state | Keys / 未注册工厂等 |

### vs 手写 fluent builder

手写可任意设计阶段接口；本域用属性 + 生成器换取统一诊断与 type-state，代价是必填上限与 MVP 同步-only。

### vs Decorator / Chain

非服务契约叠层或请求管道；仅构造期。

## 已知局限（非目标 / Phase 2+）

- **无** MVP async assemble / `Build`
- **无** MVP MSDI / Autofac / `FromServices` 步进注入
- **无** Director 框架或厚 GoF Director 层次
- **无** 互斥的 type-state 擦除（仅诊断 + 运行时拒绝）
- **无** Analyzer 主门闩或 interface-per-subset 默认编码
- **无** 合并/重命名既有注册 `*Builder` API

HTTP 请求示例已落地于 sibling [DesignPatterns.Samples#23](https://github.com/Skymly/DesignPatterns.Samples/issues/23) / [PR#24](https://github.com/Skymly/DesignPatterns.Samples/pull/24)。

## 参考

- [ADR-010](../adr/ADR-010-step-builder-type-state-markers.md) — type-state + 必填上限 8
- Spec：[Builder / Step Builder (#287)](https://github.com/Skymly/DesignPatterns/issues/287)
- 落地：#288 Runtime → #289 Diagnostics（DP078–DP086）→ #290 SourceGenerators → #291 Docs → Samples [DesignPatterns.Samples#23](https://github.com/Skymly/DesignPatterns.Samples/issues/23)
- [docs/ROADMAP.md](../ROADMAP.md) F3 Top-2
- [AGENTS.md](../../AGENTS.md) — 模式摘要与诊断表
- [FactoryRegistry.md](FactoryRegistry.md) — 注册 `FactoryRegistryBuilder` 对照
