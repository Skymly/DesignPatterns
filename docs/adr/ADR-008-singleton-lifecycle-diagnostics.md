# ADR-008: Singleton lifecycle diagnostics

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-08 |
| **关联 Issue** | —（历史 RFC / Plan / Review 已随文档体系精简移除；决策以本文为准） |

## 背景

DP062 已覆盖 MSDI 构造函数注入的 Singleton captive dependency，但 Autofac 注册与 `AddSingleton` 工厂委托不在检测范围内；`[GenerateSingleton]` 缺少 async 初始化路径，与 DI 容器单例混用时无警告；DEVELOPMENT.md 声明的「静态可变单例提示」尚无对应 Analyzer。设计评审确认了三阶段扩展方案并修正两处初稿设计错误（async 不可用属性 getter、不存在的 MSDI 字段注入）。

## 决策

分三阶段实现 Singleton 生命周期诊断（DP066–DP071）：

- **P1（Analyzers）**：Autofac 注册收集（`SingleInstance` 等，符号全名匹配、零包引用）复用 DP062；`AddSingleton` 工厂委托 lambda 内解析 Scoped/Transient → **DP066**（Warning）。
- **P2a（Runtime + SourceGenerators）**：`GenerateSingletonAttribute.InitializeAsync` 可选属性；指定时**不生成**同步 `Instance`，改为生成 `Lazy<Task<T>>` 支撑的 `GetInstanceAsync()`（两种形态互斥）；签名校验 → **DP067**（Error）。
- **P2b（Analyzers）**：`[GenerateSingleton]` 与 DI 容器双重单例 → **DP068**（Warning）；`ThreadSafe = false` 且含非 readonly 实例字段 → **DP069**（Info）。
- **P3（Analyzers）**：静态可变单例启发式 → **DP070**（Info）；与 DI Singleton 双重注册 → **DP071**（Warning）。
- 首版仅诊断，无 CodeFix；`Lazy<Task<T>>` 为生成代码内部实现，不新增公共 primitive。

## 后果

**正面**：

- 补齐 Autofac 与工厂委托两条 captive dependency 盲区，DP062 语义不变
- `[GenerateSingleton]` 获得异步一等的初始化路径，且不引入同步阻塞
- 静态可变单例反模式获得编译期提示，落实既有文档承诺

**负面**：

- 工厂委托 lambda 静态分析只覆盖直接 `GetRequiredService`/`GetService` 调用，间接调用不可达（记入 Design Doc 限制）
- Autofac 符号名匹配对 Autofac API 形状变化敏感（无编译期引用保护）
- `Lazy<Task<T>>` 默认缓存失败 Task，初始化异常后无法重试（记入 Design Doc 限制）

## 参考

- [ADR-001 Primitives over frameworks](ADR-001-primitives-over-frameworks.md)
- [ADR-004 Core does not reference MSDI](ADR-004-core-does-not-reference-msdi.md)
- [docs/ROADMAP.md](../ROADMAP.md)（Singleton 生命周期诊断条目）
