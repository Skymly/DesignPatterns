# ADR-003: Dual TFM — netstandard2.0 + net8.0

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-14 |
| **关联 RFC** | 无 — 项目创立时的兼容性决策 |

## 背景

运行时库需要选择目标框架。`netstandard2.0` 覆盖 .NET Framework / .NET Core / .NET 5+，但无法使用 `FrozenDictionary`、`Span<T>` 优化等新 API。`net8.0` 可用最新 API 但不覆盖旧运行时。

## 决策

运行时库同时目标 **`netstandard2.0` + `net8.0`**，两者均须可用并随包分发。使用 `#if` 分条件编译，`net8.0` 路径使用 `FrozenDictionary` 等优化。

## 后果

**正面**：
- 覆盖最广泛的 .NET 运行时（.NET Framework 4.6.1+ / .NET Core 2.0+ / .NET 5+）
- `net8.0` 消费者获得性能优化版本
- 元包必须同时携带两个 TFM 的 `lib`，确保消费者拿到对应版本

**负面**：
- `#if` 条件编译增加代码复杂度
- 测试需覆盖两个 TFM 路径
- 打包脚本需验证两个 `lib` 目录均存在

## 参考

- [AGENTS.md](../../AGENTS.md)「兼容基线」章节
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)「环境要求」章节
