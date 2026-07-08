# RFC: Singleton 生命周期诊断

> **状态**：Accepted
> **类型**：Feature
> **创建**：2026-07-08
> **更新**：2026-07-08（设计评审修订并 Accepted，见[评审记录](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)）
> **作者**：维护者
> **关联 Roadmap**：F3（长期探索候选 — Singleton 生命周期诊断）
> **关联 Issue**：（待创建）
> **衍生 ADR**：[ADR-008](../adr/ADR-008-singleton-lifecycle-diagnostics.md)

## 摘要

在现有 **DP062**（Singleton 构造函数捕获 Scoped/Transient 服务）基础上，分三阶段扩展 Singleton 生命周期相关编译期诊断：补齐 DI 注册覆盖缺口、增强 `[GenerateSingleton]` 生命周期安全、检测静态可变单例反模式。目标是在不引入厚重框架的前提下，最大化「Analyzer + 源生成器」协同的 DI 反模式防护价值。

## 动机

| 现状 | 局限 |
|------|------|
| **DP062** 已覆盖 MSDI `AddSingleton` / `TryAdd` 及生成器 `RegisterDi` 的**构造函数注入** captive dependency | 未覆盖 Autofac `RegisterAutofac` 注册、`AddSingleton` 工厂委托注册 |
| **`[GenerateSingleton]`** 生成 `Lazy<T>` + `Instance`，与 DI 生命周期**显式无关**（见特性 XML 文档） | 无 async 初始化路径；与 DI 混用时无警告；`ThreadSafe = false` 无场景提示 |
| **DEVELOPMENT.md** 已声明「诊断可提示静态可变单例，推荐 DI 生命周期」 | 尚无对应 Analyzer |

生产场景中 Singleton 生命周期错误（captive dependency、静态可变状态、编译期单例与容器单例混用）是高频 DI 反模式；本 RFC 将 ROADMAP 候选「Singleton 生命周期诊断」落地为可排期的三阶段计划。

## 非目标

- 不实现完整 DI 容器或替换 MSDI / Autofac。
- 不将 `[GenerateSingleton]` 与 DI 生命周期**合并**为统一单例模型（二者语义保持分离）。
- 不检测所有可能的运行时 captive dependency（如 `IServiceProvider` 手动 resolve、反射注入）。
- **Analyzer 不引用 Autofac 包**——沿用 DP060/DP062 对 MSDI 的做法：符号全名字符串匹配（`ToDisplayString()`），Analyzer 程序集保持零第三方依赖（`IncludeBuildOutput=false`）。
- 不检测字段/属性注入——MSDI 无此机制；Autofac `PropertiesAutowired()` 属性注入列为后续评估项，首版不做。
- 不重复 **DP060**（`RegisterDi` registry lifetime > implementation lifetime）或 **DP061**（wasteful lifetime mismatch）的语义。

---

## 设计方案

### 概念模型

```
┌─────────────────────────────────────────────────────────────┐
│  DI 容器注册（MSDI / Autofac / RegisterDi / RegisterAutofac）│
│  → 构建 Type → Lifetime 映射                                 │
│  → 对 Singleton 实现检查依赖来源（ctor / 工厂委托 lambda）    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  [GenerateSingleton]（编译期 Lazy<T>，非 DI）                 │
│  → 可选 async 初始化钩子                                       │
│  → 与 DI 注册混用警告                                          │
│  → ThreadSafe=false 场景提示                                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  静态可变单例反模式（无特性、无 DI）                           │
│  → 检测 public static 可变字段 / 典型 ServiceLocator 形态    │
└─────────────────────────────────────────────────────────────┘
```

### 阶段 P1 — 补齐 DP062 缺口（Analyzer 模块）

**目标**：扩展 `CaptiveDependencyAnalyzer`（或同模块姊妹 Analyzer），不修改 DP062 已发布语义。

| 增强项 | 说明 | 诊断 |
|--------|------|------|
| **Autofac 注册收集** | 识别 `RegisterType<T>().SingleInstance()`、`Register(_ => ...).SingleInstance()`、`RegisterAutofac` 生成方法内的 `SingleInstance()` / `InstancePerDependency()` 等；映射 implementation type → lifetime。检测采用**符号全名匹配**（如 `Autofac.ContainerBuilder`），不引入 Autofac 包引用 | 复用 **DP062**（同一 captive dependency 语义，构造函数注入） |
| **工厂委托注册** | `AddSingleton<T>(sp => new T(...))` / `AddSingleton(sp => ...)` 中静态分析 lambda 体内的 `GetRequiredService<X>()` / `GetService<X>()` 调用；若 `X` 在 lifetime map 中为 Scoped/Transient → 报告 | **DP066**（新，独立 ID：修复建议与构造函数场景不同） |
| **多构造函数** | 文档化限制：当前取首个 public 实例构造函数；评估是否改为「最长 public 构造函数」对齐 `ActivatorUtilities` | 无新 ID；Design Doc 记录 |

**不变量**：

- DP062 消息格式与严重性（Warning）不变。
- Analyzer 仍放在 `DesignPatterns.Analyzers`，遵守 ADR-004；对 MSDI / Autofac 均为符号名匹配，无包引用。

> 评审备注：初稿的「字段/属性注入（`[FromServices]`）」项经评审移除——MSDI 没有字段/属性注入机制，`[FromServices]` 是 ASP.NET Core MVC 参数绑定特性；见[评审记录 #2](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)。

### 阶段 P2 — `[GenerateSingleton]` 增强（SourceGenerators + Analyzers）

**目标**：增强编译期单例生成器的生命周期安全，不与 DI Singleton 混淆。

#### P2a — Async 初始化（SourceGenerators 模块）

新增可选特性属性：

```csharp
public sealed class GenerateSingletonAttribute : Attribute
{
    // 现有
    public bool ThreadSafe { get; set; } = true;

    /// <summary>
    /// Optional static method name for async initialization invoked once
    /// before the instance is returned from the generated GetInstanceAsync().
    /// When set, the synchronous Instance property is not generated.
    /// Signature: static ValueTask|Task MethodName(T instance, CancellationToken ct)
    /// </summary>
    public string? InitializeAsync { get; set; }
}
```

生成器行为（**两种形态互斥**）：

- 未指定 `InitializeAsync`（默认）：生成现有 `Lazy<T>` + 同步 `Instance` 属性，行为不变。
- 指定 `InitializeAsync`：**不生成**同步 `Instance`，改为生成：

```csharp
private static readonly Lazy<Task<T>> _instance = new(async () =>
{
    var instance = new T();
    await T.<InitializeAsync>(instance, CancellationToken.None).ConfigureAwait(false);
    return instance;
}, LazyThreadSafetyMode.ExecutionAndPublication);

/// <summary>Gets the asynchronously initialized singleton instance.</summary>
public static ValueTask<T> GetInstanceAsync() => new(_instance.Value);
```

- 生成器校验 `InitializeAsync` 方法存在、static、签名匹配 `static ValueTask|Task Method(T instance, CancellationToken ct)`（**DP067** Error，复用 State guard 签名校验基础设施）。
- `Lazy<Task<T>>` 为生成代码内部实现，不新增公共 primitive（netstandard2.0 可用；遵守 ADR-001）。

> 评审备注：初稿「`Instance` getter 首次访问时 await」不可实现（属性 getter 不能 await，同步阻塞违反异步一等原则）；改为互斥的 `GetInstanceAsync` 设计，见[评审记录 #1](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)。

#### P2b — DI 混用与 ThreadSafe 提示（Analyzers 模块）

| ID | 严重性 | 触发条件 |
|----|--------|----------|
| **DP068** | Warning | 类型同时标注 `[GenerateSingleton]` 且在编译单元内被 MSDI `AddSingleton` / `RegisterDi` / Autofac `SingleInstance` 注册（双重单例，实例不一致风险） |
| **DP069** | Info | `[GenerateSingleton(ThreadSafe = false)]` 且类型含**非 readonly 实例字段**（可变状态 + 无发布屏障 → 竞态风险） |

**非目标（P2）**：不自动生成 DI 注册代码；不将 `Instance` 注册到容器。

### 阶段 P3 — 静态可变单例反模式（Analyzers 模块）

**目标**：落实 DEVELOPMENT.md「诊断可提示静态可变单例，推荐 DI 生命周期」。

| ID | 严重性 | 触发条件 |
|----|--------|----------|
| **DP070** | Info | `public static` 非 readonly 引用类型字段，**或 `public static` 带 setter 的引用类型属性**，且类型名/成员名匹配常见单例命名（`Instance`、`Singleton`、`_instance`）或类为 `sealed` 且仅含 static 访问器 |
| **DP071** | Warning | 上述字段且类型在 DI 容器中也有 Singleton 注册（双重单例形态） |

**抑制**：`[SuppressMessage]` 或 `#pragma` 标准 Roslyn 抑制；不新增自定义 Suppress 特性。

**非目标（P3）**：不检测所有 static mutable 字段（误报控制优先）；不强制迁移到 DI。

---

## 诊断 ID 汇总

| ID | 名称 | 严重性 | 归属 | 阶段 |
|----|------|--------|------|------|
| DP062 | CaptiveDependency | Warning | Analyzers | 已有 |
| DP066 | FactoryDelegateCaptiveDependency | Warning | Analyzers | P1 |
| DP067 | GenerateSingletonInvalidInitializer | Error | SourceGenerators | P2a |
| DP068 | GenerateSingletonDiMixing | Warning | Analyzers | P2b |
| DP069 | GenerateSingletonThreadSafetyHint | Info | Analyzers | P2b |
| DP070 | StaticMutableSingleton | Info | Analyzers | P3 |
| DP071 | StaticMutableSingletonWithDi | Warning | Analyzers | P3 |

> **ID 登记**：实现前须同步 `DiagnosticIds.cs`、`DesignPatternsDiagnosticDescriptors.cs`、`AnalyzerReleases.Unshipped.md`、`AGENTS.md` 诊断表、`docs/spec/`（若影响公共 API）及 DesignPatterns.Docs diagnostics 页。

---

## API 变更

| 变更 | 类型 | 模块 |
|------|------|------|
| `GenerateSingletonAttribute.InitializeAsync` | 非破坏性新增 | Runtime (`DesignPatterns/`) |
| 无其他公共运行时 API 变更 | — | — |

Spec：若 P2a 落地，须新建或扩展 `docs/spec/Singleton.md`（当前无独立 Spec 文件）。

---

## 替代方案

| 方案 | 否决原因 |
|------|----------|
| 仅文档说明，不新增诊断 | 无法发挥编译期协同探索价值；与项目方针不符 |
| 将 `[GenerateSingleton]` 废弃，统一 DI Singleton | 破坏已有 Sample / 测试；`GenerateSingleton` 与 DI 生命周期语义 intentionally 分离 |
| 单一 mega-Analyzer 覆盖全部阶段 | 违反单模块 PR 边界；难测试、难回滚 |
| 扩展 DP062 语义覆盖工厂委托而不新 ID | 工厂委托与构造函数注入的修复建议不同（改 lambda vs 改构造函数/生命周期）；独立 ID 便于 help link 与后续 CodeFix 演进 |

---

## 开放问题

1. ~~**P2a Async 初始化**：`Lazy<Task<T>>` vs 自定义 `AsyncLazy` primitive~~ — **已决策**：采用 `Lazy<Task<T>>`（生成代码内部实现，不新增公共 primitive，ADR-001 约束）。见[评审记录 #5](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)。
2. ~~**P1 Autofac**：Analyzer 是否引用 Autofac 程序集~~ — **已决策**：符号全名字符串匹配，零包引用（对齐 DP060/DP062 的 MSDI 处理）。见[评审记录 #3](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)。
3. **P3 误报率**：`DP070` 启发式规则需在实现 PR 中用真实代码库样本调优（默认 Info 已定，调参留在 P3 实现 PR）。
4. ~~**CodeFix**~~ — **已决策**：首版仅诊断，不做 CodeFix；后续按需求迭代。见[评审记录 #7](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)。

---

## 决策记录

**2026-07-08 设计评审**（[评审记录](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)，结论：有条件通过）：

1. P2a 改为互斥 `GetInstanceAsync` 设计（原 `Instance` await 方案不可实现）。
2. P1 移除字段/属性注入项（MSDI 无此机制）；DP066 重定义为工厂委托 captive dependency。
3. Autofac 检测用符号全名匹配，Analyzer 零第三方包引用。
4. DP069 触发条件改为「非 readonly 实例字段」；DP070 启发式补充可变静态属性。
5. 首版仅诊断无 CodeFix；`Lazy<Task<T>>` 为内部实现。

**2026-07-08 维护者确认 Accepted**，产出 [ADR-008](../adr/ADR-008-singleton-lifecycle-diagnostics.md)。

---

## 参考

- [ADR-004 Core does not reference MSDI](../adr/ADR-004-core-does-not-reference-msdi.md)
- [CaptiveDependencyAnalyzer.cs](../../DesignPatterns.Analyzers/CaptiveDependencyAnalyzer.cs)（DP062 实现）
- [GenerateSingletonAttribute.cs](../../DesignPatterns/Creational/GenerateSingletonAttribute.cs)
- [ROADMAP.md](../ROADMAP.md) — Singleton 生命周期诊断候选
- [DEVELOPMENT.md](../DEVELOPMENT.md) — Singleton 诊断说明
