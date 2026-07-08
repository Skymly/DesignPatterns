# Plan: Singleton 生命周期诊断

> **状态**：Active
> **创建**：2026-07-08
> **更新**：2026-07-08
> **关联 RFC**：[docs/rfc/SingletonLifecycleDiagnostics.md](../rfc/SingletonLifecycleDiagnostics.md)
> **关联 Issue**：（待创建）
> **关联 Roadmap**：F3 — Singleton 生命周期诊断

## 目标

按 RFC 三阶段交付 Singleton 生命周期编译期诊断：P1 补齐 DP062 缺口 → P2 `[GenerateSingleton]` 增强 → P3 静态可变单例反模式检测。每阶段独立 PR，遵守 AGENTS.md 单模块边界。

## 非目标

- 不在本 Plan 内发版（发版由维护者单独决策）。
- 不在首版实现 CodeFix（除非某阶段 Review 结论要求）。

## 里程碑拆解

| 阶段 | 内容 | 模块（AGENTS.md 边界） | 状态 | PR |
|------|------|------------------------|------|-----|
| **P0** | RFC Draft → 设计评审（[Review](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)）→ Accepted → [ADR-008](../adr/ADR-008-singleton-lifecycle-diagnostics.md) | Docs | [x] | （直接提交） |
| **P1** | 扩展 captive dependency：Autofac / RegisterAutofac 注册收集（符号名匹配）、工厂委托 lambda 分析；DP066；CaptiveDependencyAnalyzer 测试 | Analyzers | [ ] | — |
| **P2a** | `GenerateSingletonAttribute.InitializeAsync` + 生成 `GetInstanceAsync`（`Lazy<Task<T>>`）+ 生成器 DP067 | Runtime + SourceGenerators（拆 2 PR） | [ ] | — |
| **P2b** | DP068 DI 混用警告 + DP069 ThreadSafe 提示 | Analyzers | [ ] | — |
| **P2** | Spec `docs/spec/Singleton.md` + Design Doc + Sample 更新（可选） | Docs | [ ] | — |
| **P3** | DP070 / DP071 静态可变单例启发式检测 | Analyzers | [ ] | — |
| **收尾** | CHANGELOG、`AGENTS.md` 诊断表、DesignPatterns.Docs diagnostics 页、RFC → Implemented 归档 | Docs | [ ] | — |

## 验收标准

- [ ] P1：Autofac `SingleInstance` 构造函数 captive + 工厂委托 captive（DP066）有 Verify 测试；DP066 已登记并发布到 `AnalyzerReleases`
- [ ] P2a：`InitializeAsync` 指定后生成 `GetInstanceAsync` 且不生成同步 `Instance`；无效签名报 DP067；netstandard2.0 / net8.0 测试通过
- [ ] P2b：同时 `[GenerateSingleton]` + `AddSingleton` 报 DP068；`ThreadSafe=false` 场景报 DP069
- [ ] P3：典型 static mutable singleton 样本报 DP070；与 DI 双重注册报 DP071；误报样本不报
- [ ] 全仓 `./build.ps1 --target Ci --configuration Release` 零警告
- [ ] RFC 状态 → Implemented；本 Plan → Done 并归档

## 风险与依赖

| 风险 | 缓解 |
|------|------|
| P1 工厂委托 lambda 静态分析覆盖不全（间接调用、方法组） | 首版仅分析直接 `GetRequiredService` / `GetService` 调用；限制记入 Design Doc |
| P2a `Lazy<Task<T>>` 初始化失败缓存异常 Task | Design Doc 记录该 `Lazy` 语义；必要时用 `LazyThreadSafetyMode.PublicationOnly` 权衡 |
| P3 启发式误报 | 默认 Info；实现 PR 用 Samples 与测试固件调参 |

## 变更记录

| 日期 | 调整 | 原因 |
|------|------|------|
| 2026-07-08 | 初版 Plan 自 RFC Draft 创建 | 用户选定完整 RFC 三阶段范围 |
| 2026-07-08 | P1 删除字段注入项（DP066 改为工厂委托 captive）；P2a 改 `GetInstanceAsync` 设计 | 设计评审 Blocker #1/#2（[Review](../review/2026-07-08-singleton-lifecycle-diagnostics-design.md)） |
