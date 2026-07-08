# Review: Singleton 生命周期诊断 — RFC 设计评审

> **状态**：Final
> **类型**：Design
> **日期**：2026-07-08
> **评审对象**：[docs/rfc/SingletonLifecycleDiagnostics.md](../rfc/SingletonLifecycleDiagnostics.md)（Draft 初版）
> **评审人**：Agent（对照代码库核查）

## 评审范围

RFC Draft 初版全部章节：P1 DP062 缺口补齐、P2 `[GenerateSingleton]` 增强、P3 静态可变单例检测、诊断 ID 分配（DP066–DP071）、开放问题 1–4。核查依据：`CaptiveDependencyAnalyzer.cs`（DP062 现实现）、`DesignPatterns.Analyzers.csproj`（依赖结构）、`SingletonSyntaxFactory.cs` / `GenerateSingletonAttribute.cs`（生成器现状）。

## 结论

**有条件通过** — 修正 2 个 Blocker、1 个 Major 后可进入 Accepted；诊断 ID 分配与三阶段结构合理，无需推翻。

## 发现

| # | 级别 | 发现 | 建议 |
|---|------|------|------|
| 1 | Blocker | P2a 设计「`Instance` getter 在首次访问时 await 初始化」不可实现——C# 属性 getter 不能 `await`；若同步阻塞（`.GetAwaiter().GetResult()`）则违反「异步一等」原则且有死锁风险 | 指定 `InitializeAsync` 时**不生成**同步 `Instance`，改为生成 `static ValueTask<T> GetInstanceAsync(CancellationToken)`，内部用 `Lazy<Task<T>>`（netstandard2.0 可用）；两种形态互斥，语义清晰 |
| 2 | Blocker | P1「字段/属性注入」以 `[FromServices]` 为触发条件不成立——`[FromServices]` 是 ASP.NET Core MVC 参数绑定特性，MSDI 本身**没有**字段/属性注入；按原设计 DP066 永远不会在纯 MSDI 代码中触发 | 首版删除字段/属性注入项；Autofac `PropertiesAutowired()` 属性注入列为「不在范围（后续评估）」。DP066 改指**工厂委托 captive dependency**（`AddSingleton<T>(sp => ...)` lambda 内解析 Scoped/Transient），该项原本悬而未决，正好补位 |
| 3 | Major | 开放问题 2（Analyzer 是否引用 Autofac 程序集）实际已有先例可循——现有 DP060/DP062 对 MSDI **未加包引用**，通过 `ToDisplayString()` 符号全名匹配（`CaptiveDependencyAnalyzer.cs:293`）；Analyzer 程序集 `IncludeBuildOutput=false`，不应携带第三方依赖 | Autofac 检测同样用**符号全名字符串匹配**（`Autofac.ContainerBuilder`、`SingleInstance` 等），不新增 PackageReference；开放问题 2 关闭 |
| 4 | Minor | DP069 触发条件「含 async 成员或实现 IDisposable」与线程安全风险相关性弱——`IDisposable` 与 `LazyThreadSafetyMode.None` 并无直接冲突 | 触发条件改为「`ThreadSafe = false` 且类型含**非 readonly 实例字段**（可变状态）」，直指竞态本质；保持 Info 级别 |
| 5 | Minor | 开放问题 1（`Lazy<Task<T>>` vs 自定义 `AsyncLazy`）可直接决策——`Lazy<Task<T>>` 在 netstandard2.0 完全可用，且本库「primitives 而非框架」原则不支持为单一场景新增公共 primitive | 采用 `Lazy<Task<T>>`（生成代码内部实现细节，不暴露公共类型）；开放问题 1 关闭 |
| 6 | Nit | DP070 启发式仅覆盖 static 字段，遗漏 `public static T Instance { get; set; }` 可变属性形态 | 启发式补充「public static 带 setter 的引用类型属性」 |
| 7 | Nit | 开放问题 4（CodeFix）建议直接定稿 | 首版仅诊断，不做 CodeFix；后续按需求迭代（与 DP010–012 处理方式一致） |

## 行动项

- [x] #1 → RFC P2a 改为 `GetInstanceAsync` 设计（本次评审随附修改）
- [x] #2 → RFC P1 重构：删除字段注入、DP066 重定义为工厂委托 captive（本次评审随附修改）
- [x] #3 → RFC 非目标与开放问题更新：Autofac 符号名匹配定稿（本次评审随附修改）
- [x] #4 → RFC DP069 触发条件修正（本次评审随附修改）
- [x] #5 → RFC 开放问题 1 关闭（本次评审随附修改）
- [x] #6 → RFC DP070 启发式补充（本次评审随附修改）
- [x] #7 → RFC 开放问题 4 关闭（本次评审随附修改）

## 参考

- [CaptiveDependencyAnalyzer.cs](../../DesignPatterns.Analyzers/CaptiveDependencyAnalyzer.cs) — DP062 符号匹配实现
- [DesignPatterns.Analyzers.csproj](../../DesignPatterns.Analyzers/DesignPatterns.Analyzers.csproj) — Analyzer 依赖结构（无 MSDI/Autofac 包引用）
- [ADR-001 Primitives over frameworks](../adr/ADR-001-primitives-over-frameworks.md)
- [ADR-004 Core does not reference MSDI](../adr/ADR-004-core-does-not-reference-msdi.md)
