# ADR-002: Roslyn incremental source generators

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-14 |
| **关联 RFC** | 无 — 项目创立时的技术选型 |

## 背景

DesignPatterns 需要在编译期减少样板代码并发现误用。可选方案：代码生成工具（T4、MSBuild task）、Roslyn 源生成器（`ISourceGenerator`）、Roslyn 增量源生成器（`IIncrementalGenerator`）。

## 决策

采用 **Roslyn 增量源生成器**（`IIncrementalGenerator` + `ForAttributeWithMetadataName`），而非 `ISourceGenerator` 或 T4。

## 后果

**正面**：
- 增量管线只在输入变化时重新执行，IDE 响应快
- `ForAttributeWithMetadataName` 精确过滤，减少不必要的语法树遍历
- 与 Roslyn 诊断管线集成，可在编译期报告 `DP###` 诊断
- 生成代码可调试（`#nullable enable`、`#pragma` 支持）

**负面**：
- 绑定 Roslyn 4.8+，限制了 VS / SDK 兼容范围
- 增量管线调试复杂度高（需用 `driver.RunGenerator` + snapshot 测试）
- `EquatableArray` 等增量安全数据结构增加了实现复杂度

## 参考

- [AGENTS.md](../../AGENTS.md)「兼容基线」章节
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)「Roslyn 组件」章节
