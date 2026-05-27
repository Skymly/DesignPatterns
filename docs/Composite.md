# Composite — 设计与实现文档

## 概述

Composite 模式将对象组织成树形结构，以统一方式对待单个对象与组合对象。本库提供 **遍历 primitives** 与 **编译期 catalog 校验**，不替代完整 UI 组件框架或持久化树存储。

## 设计目标

1. **统一节点接口**：`ICompositeNode<T>` 通过 `Children` 区分叶子与分支
2. **多种遍历策略**：深度优先（前/后序）、广度优先
3. **Catalog 装配**：`[CompositePart]` 生成 flat catalog，`BuildRoot()` 组装树
4. 不依赖 DI；树由编译期定义、运行时一次性装配

## 运行时 API（`DesignPatterns/Structural/`）

| 类型 | 说明 |
|------|------|
| `ICompositeNode<TSelf>` | 节点契约：`Children` |
| `ICompositeBuildable<TNode>` | 装配时接收子节点：`SetChildren(IReadOnlyList<TNode>)` |
| `CompositeTraverser` | 同步/异步遍历 |
| `CompositeTraversalOptions<TNode>` | 顺序、MaxDepth、仅叶子、跳过子树 |
| `CompositeTreeBuilder<TNode>` | 手动 `Leaf` / `Branch` / `Build()` |
| `CompositeCatalogEntry<TNode>` | Catalog 条目（key、parent、order、类型） |
| `CompositeCatalogAssembler` | 从 catalog 装配唯一根节点 |

## 用法

### 手动构建

```csharp
var root = new MenuBranch("Home");
var child = new MenuLeaf("Settings");

var tree = new CompositeTreeBuilder<IMenuNode>()
    .Branch(root, b => b.Leaf(child))
    .Build();

CompositeTraverser.Traverse(tree, (node, depth, _) =>
    Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"));
```

### 编译期 Catalog

```csharp
[CompositePart<IMenuNode>("root")]
public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode> { ... }

[CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode> { ... }

var root = MenuNodeCompositeCatalog.BuildRoot();
```

### 装配约定

| 要求 | 说明 |
|------|------|
| `ICompositeBuildable<TContract>` | `TContract` 为 composite 接口（如 `IMenuNode`） |
| public 无参构造 | 与 Strategy/Handler 生成器一致 |
| 唯一根 | catalog 中恰好一个 `ParentKey == null` |
| `SetChildren` 一次性 | 装配后实现类应冻结 children |

## 与 Chain / Strategy 的边界

| 模式 | 结构 | 关注点 |
|------|------|--------|
| **Composite** | 树 | 统一访问部分与整体、遍历 |
| **Chain** | 线性 pipeline | 有序执行、可短路 |
| **Strategy** | 扁平 key → 实现 | 选一个算法 |

## 示例

见 [samples/Composite.Sample](../samples/Composite.Sample/)：菜单树，`BuildRoot()` + 深度优先遍历。

## 编译期：`[CompositePart]` 源生成器

### 特性

```csharp
// 泛型（推荐，C# 11+ / net8.0）
[CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode> { }

// 非泛型（netstandard2.0）
[CompositePart("settings", typeof(IMenuNode), ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode> { }
```

### 生成输出

对每个 contract 生成（`IPaymentStrategy` → `PaymentStrategyCompositeKeys`；`IMenuNode` → `MenuNodeCompositeKeys`）：

- `{Contract}CompositeKeys` — key 常量
- `{Contract}CompositeCatalog` — entry 列表 + `BuildRoot()`

### 诊断

| ID | 说明 |
|----|------|
| DP010 | 同一 contract 下 key 重复 |
| DP011 | ParentKey 不存在 |
| DP012 | parent 链形成环 |
| DP013 | 未实现 composite contract |
| DP014 | 缺少 public 无参构造 |
| DP015 | 未实现 `ICompositeBuildable<TContract>` |

## v1 不支持

- 多根森林（需手动多次装配子树）
- 运行时动态增删节点
- DI 自动注册 `BuildRoot()` 结果

## 参考

- GoF: Composite (Structural)
- [docs/DEVELOPMENT.md](DEVELOPMENT.md)
