# ADR-006: Composite parallel traversal

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-30 |
| **关联 RFC** | [docs/rfc/archive/CompositeParallelTraversal.md](../rfc/archive/CompositeParallelTraversal.md) |

## 背景

Composite 模式已有串行遍历（`Traverse` / `TraverseAsync`），但大树遍历性能受限。需要并行遍历能力，同时保持线程安全契约清晰。

## 决策

新增 `TraverseParallel` / `TraverseParallelAsync` / `TraverseForestParallel` / `TraverseForestParallelAsync` 方法：

- **BFS 同层并行**：同一层级节点并行处理
- **DFS 子节点并行递归**：子节点并行递归，配合 `MaxParallelDepth` 超过阈值后回退串行
- **`MaxDegreeOfParallelism`** 限流
- **`AggregateException`** 聚合错误
- **`ConfigureAwait(false)`** async 路径
- **`#if` 分裂**：net8.0 用 `Parallel.ForEachAsync`，netstandard2.0 用 `SemaphoreSlim` + `Task.WhenAll`
- **线程安全责任**：用户责任 + 文档契约（不提供线程安全收集器）

## 后果

**正面**：
- 大树遍历性能显著提升
- net8.0 / netstandard2.0 双路径覆盖

**负面**：
- 用户需自行保证节点处理逻辑的线程安全
- `MaxDegreeOfParallelism = 1` 退化为串行（非并行），用户需理解此语义

## 参考

- [docs/spec/Composite.md](../spec/Composite.md)（待创建）
- [docs/design/Composite.md](../design/Composite.md)（待创建）
- [docs/Composite.md](../Composite.md)（现有模式文档，待迁移）
