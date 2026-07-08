# Spec: Composite

> **版本**：v0.2.2（与 NuGet 包版本对齐）
> **关联 Design Doc**：[docs/design/Composite.md](../design/Composite.md)
> **关联 ADR**：[ADR-006](../adr/ADR-006-composite-parallel-traversal.md)、[ADR-007](../adr/ADR-007-composite-tree-schema-validation.md)

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

## 诊断 ID

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

## 不变量

1. **单根约束（`BuildRoot`）**：`BuildRoot()` / `Assemble` 要求 catalog 中恰好一个 `ParentKey == null` 的根；零根或多根时运行时抛 `CompositeAssemblyException`。
2. **`ICompositeBuildable<TContract>` 要求**：catalog 装配的节点类型必须实现 `ICompositeBuildable<TContract>`，`TContract` 为 composite 接口（如 `IMenuNode`）。
3. **public 无参构造**：catalog 装配的节点类型必须声明 public 无参构造函数（与 Strategy/Handler 生成器一致），供 `Activator.CreateInstance` 实例化。
4. **`SetChildren` 一次性**：`SetChildren` 在装配时调用一次；实现类应在装配后冻结 children，后续调用视为无效。
5. **多根森林（`BuildForest`）**：`BuildForest()` / `AssembleForest` 允许一个或多个 `ParentKey == null` 的根，按 `Order` 升序再 key 排序返回。
6. **并行遍历顺序非确定**：`TraverseParallel` / `TraverseParallelAsync` / `TraverseForestParallel` / `TraverseForestParallelAsync` 的访问顺序非确定；需要顺序时使用 `Traverse` / `TraverseAsync`。

## 兼容基线

- `netstandard2.0` + `net8.0`（两者均须可用并随包分发）
- 泛型 `CompositePartAttribute<TContract>` 仅在 C# 11+ / net7.0+ 可用（`#if NET7_0_OR_GREATER`）；netstandard2.0 使用非泛型 `CompositePartAttribute(string key, Type @for)`
- 并行遍历：net8.0 用 `Parallel.ForEachAsync`，netstandard2.0 用 `SemaphoreSlim` + `Task.WhenAll`（`#if` 分裂）

## 不在范围内

- 运行时动态增删节点（树由编译期定义、运行时一次性装配）
- DI 自动注册 `BuildRoot()` / `BuildForest()` 结果（DI 扩展不自动注册装配产物）
