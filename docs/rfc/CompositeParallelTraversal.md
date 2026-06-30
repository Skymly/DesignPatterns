# RFC: Composite 并行遍历

> **状态**：设计完成，待实现 Phase 1
> **日期**：2026-06-30
> **关联**：[Composite.md](../Composite.md)、[ROADMAP.md](../ROADMAP.md)

## 概述

为 Composite 模式新增并行遍历能力（`TraverseParallel` / `TraverseParallelAsync`），支持 BFS 同层并行、DFS 子节点并行递归，配合 `MaxDegreeOfParallelism` 限流与 `MaxParallelDepth` 递归深度保护。

懒加载（`ICompositeLazyNode` + `[CompositePart(LazyChildren)]`）作为 Phase 3 独立 RFC，不在本纪要范围。

## 设计会议纪要

### 参与角色

| 角色 | 职责 |
|------|------|
| 运行时库架构师 | API 设计、接口契约、向后兼容性 |
| 并发工程师 | 并发原语、同步机制、性能、死锁/竞态 |
| 风险分析师 / 魔鬼代言人 | 失败模式、竞态条件、误用场景 |
| 真实用户场景分析师 | 实际使用模式、用户心智模型 |

---

## 一、达成共识的议题

### 1.1 线程安全责任归属：用户责任 + 文档契约

四角色一致同意：库不做运行时防御，线程安全是用户责任。

| 维度 | 共识 |
|------|------|
| `Children` 读取 | 组装后视为不可变；并行读取安全（前提：无写入）；不做防御性复制 |
| `ShouldSkipSubtree` | 用户责任；不做预评估（O(N) 额外遍历违背并行初衷） |
| visitor 回调 | 纯文档契约；不加 `ISafeVisitor` 标记接口 |
| `IReadOnlyList<T>` | `List<T>` 并行 `foreach` 读取安全（无写入时）；.NET 运行时保证 |

- **架构师**："库做防御性复制会破坏性能目标、掩盖用户错误。"
- **魔鬼**："库无法防止用户错误——只能通过文档和 API 设计引导正确使用。"
- **用户分析师**："模仿 `Parallel.ForEach` 的文档风格——.NET 开发者看到 `TraverseParallel` 会自然联想到线程安全要求。"

### 1.2 API 形态：独立方法

四角色一致同意：使用 `TraverseParallel` / `TraverseParallelAsync` 独立方法，不修改现有 `Traverse` 语义。

```
TraverseParallel(root, visitor, options)                    // 同步并行
TraverseParallelAsync(root, asyncVisitor, options, ct)      // 异步并行
TraverseForestParallel(roots, visitor, options)
TraverseForestParallelAsync(roots, asyncVisitor, options, ct)
```

**架构师**："`MaxDegreeOfParallelism` 仅对并行有效，加到 Options 会污染同步遍历的语义。"

### 1.3 异常传播：`AggregateException`

四角色一致同意：收集所有异常，抛 `AggregateException`。

- **并发工程师**：`ConcurrentQueue<Exception>` 收集 → `AggregateException` 抛出。
- **架构师**：提供 `ExceptionMode` 选项（Aggregate / FirstException / Suppress）。
- **魔鬼**：`ContinueOnError` 选项允许部分失败后继续。

**合并方案**：Phase 1 默认 `AggregateException`；`ContinueOnError` / `ExceptionMode` 延迟到 Phase 2。

### 1.4 async 路径：`ConfigureAwait(false)`

四角色一致同意：异步并行遍历内部使用 `ConfigureAwait(false)`。

- **魔鬼**："WPF/WinForms 中 `await` 默认捕获 `SynchronizationContext`，多个并行 visitor 同时回到 UI 线程会串行化——完全违背并行初衷。"
- **并发工程师**：`SemaphoreSlim.WaitAsync(ct).ConfigureAwait(false)` + `asyncVisitor(...).ConfigureAwait(false)`。

### 1.5 递归深度限制

四角色一致同意：限制并行递归深度，超过阈值回退到顺序遍历。

- **并发工程师**：`MaxParallelDepth = 32`，超过后顺序递归。
- **魔鬼**：并行递归 DFS 在循环引用"树"中会创建无限 Task，耗尽线程池——但循环检测默认关闭（开销大）。

### 1.6 netstandard2.0 兼容

四角色一致同意：`#if` 分支。

| TFM | 同步并行 | 异步并行 |
|-----|----------|----------|
| net8.0 | `Parallel.ForEach` | `Parallel.ForEachAsync`（.NET 6+） |
| netstandard2.0 | `Parallel.ForEach` | `SemaphoreSlim` + `Task.WhenAll` |

---

## 二、存在分歧的议题

### 2.1 `MaxDegreeOfParallelism = 1` 的行为

| 角色 | 立场 |
|------|------|
| 魔鬼 | 抛 `ArgumentException` 或文档警告"不保证顺序" |
| 架构师 | 不特殊处理，用户应直接用 `Traverse` |
| 并发工程师 | 不特殊处理，`Parallel.ForEach` 的 `MaxDop=1` 已有定义 |

**决议**：不抛异常，文档明确"`MaxDegreeOfParallelism = 1` 不保证顺序；如需顺序请用 `Traverse`"。

### 2.2 是否提供线程安全收集器

| 角色 | 立场 |
|------|------|
| 用户分析师 | **必须提供** `CollectParallel` API + `CompositeTraversalCollector<T>` |
| 架构师 | 纯文档契约，不加额外 API |
| 魔鬼 | 提供收集器重载，但不强制 |
| 并发工程师 | 不涉及（超出线程安全范围） |

**用户分析师的关键论据**：

```csharp
// 用户最自然的写法——会崩溃
var errors = new List<string>();
CompositeTraverser.TraverseParallel(root, (node, _, _) => {
    if (!node.IsValid()) errors.Add($"{node.Key}: invalid");
});
```

**决议**：Phase 1 不提供收集器（保持 API 最小化）；文档示例展示 `ConcurrentBag<T>` 用法；Phase 2 根据用户反馈再考虑 `CollectParallel`。

### 2.3 `ExceptionMode` vs `ContinueOnError`

| 角色 | 立场 |
|------|------|
| 架构师 | `ParallelExceptionMode` 枚举（Aggregate / First / Suppress） |
| 魔鬼 | `ContinueOnError` 布尔选项 |
| 并发工程师 | 默认 `AggregateException`，不需要选项 |

**决议**：Phase 1 只实现 `AggregateException`（默认行为）。`ContinueOnError` / `ExceptionMode` 延迟到 Phase 2 根据用户反馈再加。

### 2.4 默认并行度

| 角色 | 立场 |
|------|------|
| 魔鬼 | `Math.Min(ProcessorCount, 4)` 保守默认 |
| 架构师 | `Environment.ProcessorCount` |
| 并发工程师 | `Environment.ProcessorCount` |
| 用户分析师 | `Environment.ProcessorCount`（与 `Parallel.ForEach` 一致） |

**魔鬼的关键论据**：Docker 容器中 `ProcessorCount` 可能返回宿主机 CPU 数而非容器限制。

**决议**：默认 `Environment.ProcessorCount`（与 .NET 生态一致）；文档警告容器化环境需手动设置。

### 2.5 循环检测

| 角色 | 立场 |
|------|------|
| 魔鬼 | 提供 `DetectCycles` 选项（默认关闭） |
| 其他三角色 | 不需要（树结构假设无循环） |

**决议**：不实现循环检测。文档明确"Composite 树必须无循环引用"。顺序遍历的栈溢出是快速失败，已足够。

### 2.6 测试可重复性

| 角色 | 立场 |
|------|------|
| 魔鬼 | `ForceSequentialForTesting` 选项 |
| 架构师 | 不需要选项，测试用 `ConcurrentBag` + count 断言 |
| 并发工程师 | `MaxDegreeOfParallelism = 1` 用于逻辑测试 |

**决议**：不提供 `ForceSequentialForTesting`。测试策略：
- 逻辑正确性：`ConcurrentBag<T>` + count + 集合断言（不断言顺序）
- 竞态测试：`MaxDegreeOfParallelism = Environment.ProcessorCount` + 多次运行

---

## 三、最终技术方案

### 3.1 `CompositeTraversalOptions` 新增属性

```csharp
/// <summary>
/// Maximum degree of parallelism for parallel traversals.
/// null means Environment.ProcessorCount.
/// WARNING: Setting to 1 does NOT guarantee visitation order;
/// use Traverse() for ordered traversal.
/// </summary>
public int? MaxDegreeOfParallelism { get; set; }

/// <summary>
/// Maximum depth for parallel recursion. Beyond this depth,
/// traversal falls back to sequential. Default is 32.
/// </summary>
public int MaxParallelDepth { get; set; } = 32;
```

### 3.2 同步并行实现

| 遍历顺序 | 策略 |
|----------|------|
| BFS | 逐层提取 → `Parallel.ForEach` 同层并行 → 收集下一层 |
| DFS PreOrder | 根节点同步访问 → 子节点 `Parallel.ForEach` 并行递归 |
| DFS PostOrder | 子节点 `Parallel.ForEach` 并行递归 → 全部完成后访问根节点 |
| 深度 > MaxParallelDepth | 回退到顺序递归 |

### 3.3 异步并行实现

| TFM | 策略 |
|-----|------|
| net8.0 | `Parallel.ForEachAsync` + `ConfigureAwait(false)` |
| netstandard2.0 | `SemaphoreSlim` + `Task.WhenAll` + `ConfigureAwait(false)` |

### 3.4 异常处理

```csharp
var exceptions = new ConcurrentQueue<Exception>();

// 每个 visitor 调用:
try { visitor(node, depth, idx); }
catch (Exception ex) { exceptions.Enqueue(ex); }

// 遍历结束后:
if (!exceptions.IsEmpty)
    throw new AggregateException(exceptions);
```

### 3.5 取消传播

- 直接传递 `CancellationToken`，不使用 `CreateLinkedTokenSource`
- `Parallel.ForEach` / `SemaphoreSlim.WaitAsync` 自动响应取消
- 已启动的 visitor 继续完成；未启动的任务取消

### 3.6 文档契约（XML doc）

```
TraverseParallel:
  "The visitor may be invoked concurrently from multiple threads.
   Ensure thread safety: use ConcurrentBag<T> for accumulation,
   lock/SemaphoreSlim for shared state. Order is non-deterministic;
   use Traverse() for ordered traversal."

MaxDegreeOfParallelism:
  "null = Environment.ProcessorCount. Setting to 1 does NOT
   guarantee order. In containerized environments, manually set
   this to match CPU limits."
```

---

## 四、风险矩阵与决议

| 风险 | 严重度 | 库层面解决 | 决议 |
|------|--------|-----------|------|
| visitor 共享状态崩溃 | 高 | 否（用户责任） | 文档警告 + `ConcurrentBag` 示例 |
| `ShouldSkipSubtree` 竞态 | 中 | 否（用户责任） | 文档警告 |
| `Children` 懒加载非线程安全 | 高 | 否（用户责任） | 文档要求组装后不可变 |
| 死锁 | 中 | 库内部无锁 | 文档警告避免 visitor+skip 共享锁 |
| `MaxDop=1` 误导 | 中 | 文档 | 不抛异常，文档明确 |
| async `SynchronizationContext` | 高 | 是 | `ConfigureAwait(false)` |
| 异常资源泄漏 | 中 | 否（用户责任） | 文档警告 + `using` |
| 循环引用 | 高 | 否 | 文档要求无循环 |
| 容器 `ProcessorCount` | 中 | 部分 | 文档警告 |
| 测试可重复性 | 中 | 否 | 测试策略指南 |

---

## 五、行动计划

| Phase | 内容 | 优先级 |
|-------|------|--------|
| **Phase 1** | `TraverseParallel` + `TraverseParallelAsync` + `TraverseForestParallel` + `TraverseForestParallelAsync` + `MaxDegreeOfParallelism` + `MaxParallelDepth` + `AggregateException` + `ConfigureAwait(false)` + 文档 + 测试 | 立即 |
| **Phase 2** | `ContinueOnError` 选项 + `CollectParallel` API + `ExceptionMode` 枚举 | 根据用户反馈 |
| **Phase 3** | 懒加载（`ICompositeLazyNode` + `[CompositePart(LazyChildren)]` + 生成器） | 独立 RFC |

---

## 六、用户场景分析摘要

### 适合并行遍历的场景

| 场景 | 原因 |
|------|------|
| 配置树校验（CPU 密集） | 各节点校验独立，并行收益明显 |
| 远程 API 调用（I/O 密集 async） | 并发度从 1 提升到 N，延迟降低数量级 |
| 日志/遥测收集 | `ILogger` 实现线程安全 |
| Forest 遍历（多棵独立树） | 最安全的并行场景，无共享状态 |
| DI 容器解析 | `IServiceProvider` 线程安全 |

### 不适合并行遍历的场景

| 场景 | 原因 |
|------|------|
| UI 菜单树渲染 | UI dispatcher 单线程瓶颈，并行收益有限 |
| 写入同一个 `Stream` | `Stream.Write` 非线程安全 |
| 向 `StringBuilder` 追加 | `StringBuilder` 非线程安全 |
| 向 `List<T>.Add` 收集 | `List<T>.Add` 非线程安全（最常见误用） |

### 用户最常见误用模式

```csharp
// ❌ 会崩溃——List<T>.Add 非线程安全
var results = new List<string>();
CompositeTraverser.TraverseParallel(root, (node, _, _) => results.Add(node.Name));

// ✅ 正确——ConcurrentBag<T> 线程安全
var results = new ConcurrentBag<string>();
CompositeTraverser.TraverseParallel(root, (node, _, _) => results.Add(node.Name));
```
