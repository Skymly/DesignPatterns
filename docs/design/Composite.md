# Design Doc: Composite

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 ADR**：[ADR-006](../adr/ADR-006-composite-parallel-traversal.md)、[ADR-007](../adr/ADR-007-composite-tree-schema-validation.md)

## 概述

Composite 模式将对象组织成树形结构，以统一方式对待单个对象与组合对象。本库提供 **遍历 primitives** 与 **编译期 catalog 校验**，不替代完整 UI 组件框架或持久化树存储。

## 设计目标

1. **统一节点接口**：`ICompositeNode<T>` 通过 `Children` 区分叶子与分支
2. **多种遍历策略**：深度优先（前/后序）、广度优先
3. **Catalog 装配**：`[CompositePart]` 生成 flat catalog，`BuildRoot()` 组装单根树，`BuildForest()` 组装多根森林
4. 不依赖 DI；树由编译期定义、运行时一次性装配

## API 面

### 运行时接口

命名空间：`DesignPatterns.Structural`

| 类型 | 签名 | 说明 |
|------|------|------|
| `ICompositeNode<TSelf>` | `IReadOnlyList<TSelf> Children { get; }` where `TSelf : ICompositeNode<TSelf>` | 节点契约：通过 `Children` 区分叶子与分支，空列表为叶子 |
| `ICompositeBuildable<TNode>` | `void SetChildren(IReadOnlyList<TNode> children)` where `TNode : ICompositeNode<TNode>` | 装配时接收子节点；实现应在装配后冻结 children，后续调用视为无效 |
| `CompositeTraverser` | `static void Traverse<TNode>(TNode root, Action<TNode, int, int> visitor, CompositeTraversalOptions<TNode>? options = null)` | 同步遍历单根树（visitor: node, depth, siblingIndex） |
| | `static void TraverseForest<TNode>(IReadOnlyList<TNode> roots, Action<TNode, int, int> visitor, CompositeTraversalOptions<TNode>? options = null)` | 同步遍历森林（多根树） |
| | `static ValueTask TraverseAsync<TNode>(TNode root, Func<TNode, int, int, CancellationToken, ValueTask> visitor, CompositeTraversalOptions<TNode>? options = null, CancellationToken cancellationToken = default)` | 异步遍历单根树 |
| | `static ValueTask TraverseForestAsync<TNode>(IReadOnlyList<TNode> roots, Func<TNode, int, int, CancellationToken, ValueTask> visitor, CompositeTraversalOptions<TNode>? options = null, CancellationToken cancellationToken = default)` | 异步遍历森林 |
| | `static void TraverseParallel<TNode>(TNode root, Action<TNode, int, int> visitor, CompositeTraversalOptions<TNode>? options = null)` | 并行遍历单根树（访问顺序非确定） |
| | `static void TraverseForestParallel<TNode>(IReadOnlyList<TNode> roots, Action<TNode, int, int> visitor, CompositeTraversalOptions<TNode>? options = null)` | 并行遍历森林（根间串行，子树内并行） |
| | `static ValueTask TraverseParallelAsync<TNode>(TNode root, Func<TNode, int, int, CancellationToken, ValueTask> visitor, CompositeTraversalOptions<TNode>? options = null, CancellationToken cancellationToken = default)` | 异步并行遍历单根树 |
| | `static ValueTask TraverseForestParallelAsync<TNode>(IReadOnlyList<TNode> roots, Func<TNode, int, int, CancellationToken, ValueTask> visitor, CompositeTraversalOptions<TNode>? options = null, CancellationToken cancellationToken = default)` | 异步并行遍历森林 |
| `CompositeTraversalOptions<TNode>` | `CompositeTraversalOrder Order { get; set; }`（默认 `DepthFirstPreOrder`） | 遍历顺序 |
| | `int? MaxDepth { get; set; }`（`null` = 无限制） | 最大访问深度（0 = 仅根） |
| | `bool VisitLeavesOnly { get; set; }` | 仅访问叶子节点 |
| | `Func<TNode, bool>? ShouldSkipSubtree { get; set; }` | 返回 `true` 时跳过该子树 |
| | `int? MaxDegreeOfParallelism { get; set; }`（`null` = `Environment.ProcessorCount`） | 并行度上限 |
| | `int MaxParallelDepth { get; set; }`（默认 32） | 并行递归深度上限，超过后回退串行 |
| `CompositeTreeBuilder<TNode>` | `CompositeTreeBuilder<TNode> Leaf(TNode node)` | 添加叶子节点 |
| | `CompositeTreeBuilder<TNode> Branch(TNode node, Action<CompositeTreeBuilder<TNode>> configure)` | 添加分支节点（嵌套 builder 配置子节点） |
| | `TNode Build()` | 构建单根树（要求恰好一个顶层节点） |
| `CompositeCatalogEntry<TNode>` | `CompositeCatalogEntry(string key, string? parentKey, int order, Type implementationType)` | Catalog 条目 |
| | `string Key { get; }` | 唯一键 |
| | `string? ParentKey { get; }` | 父键（`null` 为根候选） |
| | `int Order { get; }` | 同父兄弟间排序（升序） |
| | `Type ImplementationType { get; }` | 实现类型 |
| `CompositeCatalogAssembler` | `static TNode Assemble<TNode>(IReadOnlyList<CompositeCatalogEntry<TNode>> entries)` where `TNode : class, ICompositeNode<TNode>` | 从 catalog 装配单根树（`Activator.CreateInstance`） |
| | `static TNode Assemble<TNode>(IReadOnlyList<CompositeCatalogEntry<TNode>> entries, IServiceProvider serviceProvider)` | 从 catalog 装配单根树（DI 解析节点） |
| | `static IReadOnlyList<TNode> AssembleForest<TNode>(IReadOnlyList<CompositeCatalogEntry<TNode>> entries)` | 从 catalog 装配多根森林（`Activator.CreateInstance`） |
| | `static IReadOnlyList<TNode> AssembleForest<TNode>(IReadOnlyList<CompositeCatalogEntry<TNode>> entries, IServiceProvider serviceProvider)` | 从 catalog 装配多根森林（DI 解析节点） |

`CompositeTraversalOrder` 枚举值：`DepthFirstPreOrder`、`DepthFirstPostOrder`、`BreadthFirst`。

### 特性（Attribute）

命名空间：`DesignPatterns.Structural`

#### `CompositePartAttribute`（非泛型，netstandard2.0）

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositePartAttribute : Attribute
{
    public CompositePartAttribute(string key, Type @for);

    public string Key { get; }
    public Type For { get; }
    public string? ParentKey { get; set; }   // null = 根候选
    public int Order { get; set; }            // 同父兄弟间排序，升序
    public Type[]? AllowedChildTypes { get; set; }  // 允许的子节点实现类型；null = 不限制
}
```

#### `CompositePartAttribute<TContract>`（泛型，C# 11+ / net8.0）

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositePartAttribute<TContract> : Attribute
{
    public CompositePartAttribute(string key);

    public string Key { get; }
    public string? ParentKey { get; set; }
    public int Order { get; set; }
    public Type[]? AllowedChildTypes { get; set; }
}
```

#### `CompositeSchemaAttribute`（契约级约束）

```csharp
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositeSchemaAttribute : Attribute
{
    public int MaxDepth { get; set; }   // 最大树深度（root = 1）；0 = 无限制；默认 0
    public int MaxNodes { get; set; }   // 最大节点总数（所有根合计）；0 = 无限制；默认 0
}
```

标注于 composite 契约接口或基类，启用编译期树结构 schema 校验。未标注时行为不变。

### 生成器产出

源生成器 `CompositePartGenerator` 对每个 composite contract 生成以下类型（`IPaymentStrategy` → `PaymentStrategyCompositeKeys`；`IMenuNode` → `MenuNodeCompositeKeys`）：

| 生成类型 | 说明 |
|----------|------|
| `{Contract}CompositeKeys` | key 常量（`public const string`） |
| `{Contract}CompositeCatalog` | entry 列表 + `BuildRoot()` + `BuildForest()` 方法 |

`{Contract}CompositeCatalog` 方法：

| 方法 | 说明 |
|------|------|
| `BuildRoot()` | 从 catalog 装配单根树；要求 catalog 中**恰好一个** `ParentKey == null` 的根，否则运行时抛 `CompositeAssemblyException` |
| `BuildForest()` | 从 catalog 装配多根森林；一个或多个 `ParentKey == null`，按 `Order` 再 key 排序 |

## 诊断

| ID | 级别 | 触发条件 | 消息格式 |
|----|------|----------|----------|
| DP010 | Error | 同一 contract 下 key 重复 | `Composite key '{0}' is already registered for contract '{1}'. Use a unique key or remove the duplicate [CompositePart] attribute.` |
| DP011 | Error | `ParentKey` 引用的 key 不存在 | `Composite parent key '{0}' was not found for contract '{1}'. Register the parent part first or correct the ParentKey value.` |
| DP012 | Error | parent 链形成环 | `Composite key '{0}' participates in a parent-key cycle for contract '{1}'. Remove or reassign ParentKey values to break the cycle.` |
| DP013 | Error | 标注类型未实现 composite contract | `Type '{0}' does not implement composite contract '{1}'. Implement the contract or fix the [CompositePart] contract argument.` |
| DP014 | Error | 缺少 public 无参构造 | `Type '{0}' must declare a public parameterless constructor for generated composite catalogs.` |
| DP015 | Error | 未实现 `ICompositeBuildable<TContract>` | `Type '{0}' must implement ICompositeBuildable<{1}> to be used with generated composite catalogs.` |
| DP040 | Error | `BuildRoot(IServiceProvider)` 时节点类型未注册到 DI 容器 | `Composite node type '{0}' was not registered in the service collection. Call {1}.RegisterDi(services) before BuildRoot(serviceProvider), or register the type manually.` |
| DP041 | Info | Visitor 覆盖不全（保留 ID — C# 编译器通过接口实现 CS0535 自动强制覆盖，诊断不实际触发） | `Visitor '{0}' does not implement all Visit methods of '{1}'. The C# compiler enforces full coverage via interface implementation (CS0535); this diagnostic is reserved for future use.` |
| DP063 | Warning | `[CompositeSchema(MaxDepth)]` 约束被超过 | `Composite tree for contract '{0}' has depth {1}, exceeding the maximum depth of {2} declared by [CompositeSchema]. Consider flattening the tree structure or increasing MaxDepth.` |
| DP064 | Error | 子节点实现类型不在父节点 `AllowedChildTypes` 集合中 | `Composite part '{0}' (type '{1}') is not in the AllowedChildTypes of its parent '{2}' (type '{3}'). Add '{1}' to the parent's AllowedChildTypes or change the ParentKey to a compatible parent.` |
| DP065 | Warning | `[CompositeSchema(MaxNodes)]` 约束被超过 | `Composite contract '{0}' has {1} parts, exceeding the maximum of {2} declared by [CompositeSchema]. Consider splitting into multiple contracts or increasing MaxNodes.` |

## 不变量 / 兼容基线

1. **单根约束（`BuildRoot`）**：`BuildRoot()` / `Assemble` 要求 catalog 中恰好一个 `ParentKey == null` 的根；零根或多根时运行时抛 `CompositeAssemblyException`。
2. **`ICompositeBuildable<TContract>` 要求**：catalog 装配的节点类型必须实现 `ICompositeBuildable<TContract>`，`TContract` 为 composite 接口（如 `IMenuNode`）。
3. **public 无参构造**：catalog 装配的节点类型必须声明 public 无参构造函数（与 Strategy/Handler 生成器一致），供 `Activator.CreateInstance` 实例化。
4. **`SetChildren` 一次性**：`SetChildren` 在装配时调用一次；实现类应在装配后冻结 children，后续调用视为无效。
5. **多根森林（`BuildForest`）**：`BuildForest()` / `AssembleForest` 允许一个或多个 `ParentKey == null` 的根，按 `Order` 升序再 key 排序返回。
6. **并行遍历顺序非确定**：`TraverseParallel` / `TraverseParallelAsync` / `TraverseForestParallel` / `TraverseForestParallelAsync` 的访问顺序非确定；需要顺序时使用 `Traverse` / `TraverseAsync`。

### 兼容基线

- `netstandard2.0` + `net8.0`（两者均须可用并随包分发）
- 泛型 `CompositePartAttribute<TContract>` 仅在 C# 11+ / net7.0+ 可用（`#if NET7_0_OR_GREATER`）；netstandard2.0 使用非泛型 `CompositePartAttribute(string key, Type @for)`
- 并行遍历：net8.0 用 `Parallel.ForEachAsync`，netstandard2.0 用 `SemaphoreSlim` + `Task.WhenAll`（`#if` 分裂）

## 实现概览

### 运行时

#### 树遍历（DFS / BFS）

`CompositeTraverser` 是静态类，提供同步 / 异步 / 并行遍历单根树与森林的方法。遍历顺序由 `CompositeTraversalOrder` 枚举控制：

- **`DepthFirstPreOrder`**：先访问节点，再递归子节点
- **`DepthFirstPostOrder`**：先递归子节点，再访问节点
- **`BreadthFirst`**：逐层访问

`CompositeTraversalOptions<TNode>` 提供运行时控制：

- `MaxDepth`：限制访问深度（0 = 仅根，`null` = 无限制）
- `VisitLeavesOnly`：仅将叶子节点传给 visitor
- `ShouldSkipSubtree`：谓词返回 `true` 时跳过该子树

visitor 签名：`Action<TNode, int, int>`（node, depth, siblingIndex）；异步版本 `Func<TNode, int, int, CancellationToken, ValueTask>`。

#### 并行遍历

`TraverseParallel` / `TraverseParallelAsync` / `TraverseForestParallel` / `TraverseForestParallelAsync` 提供并行遍历能力（详见 [ADR-006](../adr/ADR-006-composite-parallel-traversal.md)）：

- **BFS 同层并行**：同一层级节点并行处理
- **DFS 子节点并行递归**：子节点并行递归，配合 `MaxParallelDepth`（默认 32）超过阈值后回退串行，避免过度任务创建
- **`MaxDegreeOfParallelism`** 限流（`null` = `Environment.ProcessorCount`）
- **`AggregateException`** 聚合错误：visitor 异常收集到 `ConcurrentQueue<Exception>`，遍历结束后统一抛出
- **`ConfigureAwait(false)`** async 路径
- **`#if` 分裂**：net8.0 用 `Parallel.ForEachAsync`，netstandard2.0 用 `SemaphoreSlim` + `Task.WhenAll`
- **线程安全责任**：用户责任 + 文档契约（不提供线程安全收集器）；建议使用 `ConcurrentBag<T>` 累积、`lock` / `SemaphoreSlim` 保护共享状态
- **森林并行**：根间串行遍历，并行性在每个根的子树内；异常跨所有根收集后统一抛出

#### 森林支持

`TraverseForest` / `TraverseForestAsync` / `TraverseForestParallel` / `TraverseForestParallelAsync` 接受 `IReadOnlyList<TNode> roots`，对多根森林遍历。visitor 的第三个参数 `siblingIndex` 在森林模式下表示 root index。

`CompositeCatalogAssembler.AssembleForest` 从 flat catalog 装配多根森林：根按 `Order` 升序再 key 排序返回。

#### 手动构建

`CompositeTreeBuilder<TNode>` 提供无 catalog 元数据的手动树构建：

```csharp
var root = new MenuBranch("Home");
var child = new MenuLeaf("Settings");

var tree = new CompositeTreeBuilder<IMenuNode>()
    .Branch(root, b => b.Leaf(child))
    .Build();

CompositeTraverser.Traverse(tree, (node, depth, _) =>
    Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"));
```

`Build()` 要求恰好一个顶层节点（`Leaf` 或 `Branch` 添加），否则抛 `InvalidOperationException`。`Branch` 通过嵌套 builder 配置子节点，内部调用 `ICompositeBuildable<TNode>.SetChildren` 注入子节点。

### 源生成器

#### Flat catalog 从 `[CompositePart]` 生成

`CompositePartGenerator`（`IIncrementalGenerator`）通过 `ForAttributeWithMetadataName` 同时收集非泛型 `CompositePartAttribute` 与泛型 `CompositePartAttribute<TContract>` 标注的类。增量管线阶段：

1. **Transform**：从 attribute 参数提取 key、parentKey、order、contract、allowedChildTypes、schema 约束；校验实现类型是否实现 contract、是否有 public 无参构造、是否实现 `ICompositeBuildable<TContract>`
2. **Collect + Combine**：按 contract `FullyQualifiedName` 分组
3. **Execute**：对每组报告诊断（DP010–DP015, DP063–DP065），过滤有效注册，生成 `{Contract}CompositeKeys` + `{Contract}CompositeCatalog`

```csharp
[CompositePart<IMenuNode>("root")]
public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode> { ... }

[CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode> { ... }

var root = MenuNodeCompositeCatalog.BuildRoot();
```

非泛型变体（netstandard2.0）：

```csharp
[CompositePart("settings", typeof(IMenuNode), ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode> { }
```

#### 树装配

`{Contract}CompositeCatalog` 内部持有 `CompositeCatalogEntry<TNode>` 列表，`BuildRoot()` / `BuildForest()` 委托 `CompositeCatalogAssembler.Assemble` / `AssembleForest` 装配：

- 从 flat parent-key 映射构建 `entryByKey` + `childrenByParent` 字典
- 根 = `ParentKey == null` 的条目，按 `Order` 再 key 排序
- 递归 `AssembleSubtree`：先递归创建子节点，再创建当前节点并调用 `SetChildren` 注入已创建的子节点列表
- 实例化方式：`Activator.CreateInstance(type)` 或 `IServiceProvider.GetService(type)`（DI 重载）

#### Schema 校验

`[CompositeSchema(MaxDepth, MaxNodes)]` 标注于契约接口/基类时，源生成器在编译期从 flat parent-key 映射重建完整树拓扑并执行结构校验（详见 [ADR-007](../adr/ADR-007-composite-tree-schema-validation.md)）：

- **深度检查（DP063）**：计算树的实际深度，与 `MaxDepth` 比较，超限报告 Warning
- **类型检查（DP064）**：父节点声明 `AllowedChildTypes` 时，校验每个子节点的实现类型是否在允许集合中，不在则报告 Error
- **计数检查（DP065）**：统计所有 `[CompositePart]` 节点总数，与 `MaxNodes` 比较，超限报告 Warning

所有 schema 约束均为 opt-in — 未标注 `[CompositeSchema]` 的契约行为不变。校验在编译期完成，运行时零开销。

### 诊断检测细节

诊断检测逻辑与报告位置：

| ID | 检测逻辑 | 报告位置 |
|----|----------|----------|
| DP010 | 按 contract 分组后按 key 分组，`Count > 1` 的组中每个注册报告 | `[CompositePart]` attribute 位置 |
| DP011 | 收集所有 key 到 `HashSet`，`ParentKey != null && !keys.Contains(ParentKey)` | `[CompositePart]` attribute 位置 |
| DP012 | 构建 parent map，对每个 key 检测是否参与环 | `[CompositePart]` attribute 位置 |
| DP013 | `ImplementsContract` 为 false（符号检查实现接口/继承基类） | `[CompositePart]` attribute 位置 |
| DP014 | `HasPublicParameterlessConstructor` 为 false | `[CompositePart]` attribute 位置 |
| DP015 | `ImplementsBuildable` 为 false（未实现 `ICompositeBuildable<TContract>`） | `[CompositePart]` attribute 位置 |
| DP040 | `BuildRoot(IServiceProvider)` 时节点类型未注册到容器 | 生成器报告 |
| DP041 | 保留 ID — C# 编译器 CS0535 自动强制接口覆盖，诊断不实际触发 | — |
| DP063 | 编译期计算树深度，与 `[CompositeSchema(MaxDepth)]` 比较 | `[CompositeSchema]` 位置 |
| DP064 | 编译期检查子节点实现类型是否在父节点 `AllowedChildTypes` 集合中 | `[CompositePart]` attribute 位置 |
| DP065 | 编译期统计节点总数，与 `[CompositeSchema(MaxNodes)]` 比较 | `[CompositeSchema]` 位置 |

## 设计权衡

### Flat catalog 而非嵌套结构

选择 flat catalog（`CompositeCatalogEntry` 列表 + parent-key 映射）而非嵌套 attribute 结构，因为：

- flat catalog 易于源生成器收集与去重
- parent-key 映射天然支持环检测、孤儿检测
- 用户无需深层嵌套标注，`[CompositePart]` 标注独立类即可
- 运行时装配通过字典查找 O(1) 定位父子关系

### `BuildRoot` vs `BuildForest`

同时提供单根 `BuildRoot()` 与多根 `BuildForest()`：

- `BuildRoot()` 强制单根约束（恰好一个 `ParentKey == null`），运行时抛 `CompositeAssemblyException` 明确失败
- `BuildForest()` 允许零个或多个根，按 `Order` 再 key 排序返回
- 用户根据场景选择；多根 catalog 调用 `BuildRoot()` 会明确报错而非静默取第一个

### 并行遍历设计

详见 [ADR-006](../adr/ADR-006-composite-parallel-traversal.md)：

- **BFS 同层并行**：同一层级节点并行处理，适合宽树
- **DFS 子节点并行递归**：子节点并行递归，配合 `MaxParallelDepth`（默认 32）超过阈值后回退串行，避免过度任务创建导致调度开销
- **`MaxDegreeOfParallelism = 1` 不保证顺序**：退化为串行但非有序；需要顺序时使用 `Traverse` / `TraverseAsync`
- **森林并行**：根间串行（保持 root 顺序），并行性在每个根的子树内
- **线程安全**：用户责任 + 文档契约，不提供线程安全收集器

## 与生态的边界

| 模式 | 结构 | 关注点 |
|------|------|--------|
| **Composite** | 树 | 统一访问部分与整体、遍历 |
| **Chain** | 线性 pipeline | 有序执行、可短路 |
| **Strategy** | 扁平 key → 实现 | 选一个算法 |

Composite 关注树形结构与统一遍历；Chain 关注线性有序执行与短路；Strategy 关注扁平 key 到单一实现的分发。三者结构不同，不存在替代关系。

## 已知局限

- **无运行时动态节点**：树由编译期 `[CompositePart]` 定义、运行时一次性装配，不支持运行时动态增删节点
- **无 DI 自动注册**：`BuildRoot()` / `BuildForest()` 结果不自动注册到 DI 容器；DI 扩展不处理装配产物
- **并行遍历顺序非确定**：`TraverseParallel` 系列方法访问顺序非确定，需要顺序时使用串行 `Traverse` / `TraverseAsync`
- **容器化环境 CPU 计数**：`MaxDegreeOfParallelism = null` 时使用 `Environment.ProcessorCount`，在 Docker/Kubernetes 中可能返回宿主机 CPU 数而非容器限制，需手动设置

## 参考

- GoF: Composite (Structural)
- [ADR-006: Composite parallel traversal](../adr/ADR-006-composite-parallel-traversal.md)
- [ADR-007: Composite tree schema validation](../adr/ADR-007-composite-tree-schema-validation.md)
- [docs/DEVELOPMENT.md](../DEVELOPMENT.md)
- 示例：[DesignPatterns.Samples.Composite](https://github.com/Skymly/DesignPatterns.Samples/tree/main/DesignPatterns.Samples.Composite)
  - **Catalog 森林**：`MenuNodeCompositeCatalog.BuildForest()` + `TraverseForest`（多根 `[CompositePart]`；多根时 `BuildRoot()` 抛错）
  - **手动**：`CompositeTreeBuilder<IMenuNode>()` 的 `Leaf` / `Branch` / `Build()`（`ManualMenuNodes`，无 catalog 特性）
