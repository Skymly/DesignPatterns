# RFC: Composite 树 schema 校验

> **状态**：Implemented
> **类型**：Feature
> **创建**：2026-07-15
> **更新**：2026-07-07
> **作者**：维护者
> **关联 Roadmap**：F5+
> **关联 Issue**：#218
> **衍生 ADR**：[ADR-007](../../adr/ADR-007-composite-tree-schema-validation.md)

## 概述

为 Composite 模式新增编译期树结构 schema 校验：**最大深度**、**父子类型兼容性**、**节点计数**。在源生成器阶段从 flat parent-key 映射计算树结构并报告诊断，无需运行时开销。

现有 Composite 诊断（DP010–DP015, DP040–DP041）覆盖键唯一性、父键存在性、循环检测、契约实现、构造函数、DI 注册等基础校验，但**不**校验树的整体结构形态。本 RFC 补齐这一层。

## 设计目标

| 目标 | 说明 |
|------|------|
| 编译期捕获结构错误 | 深度过大可能导致栈溢出；类型不匹配的父子关系是设计错误；节点过多暗示架构问题 |
| 零运行时开销 | 所有校验在源生成器阶段完成，不增加运行时成本 |
| 向后兼容 | 所有新约束均为 opt-in，不标注则不校验（现有代码行为不变） |
| 探索价值 | 展示「源生成器从 flat 声明重建树拓扑并在编译期校验」的编译期 + 运行时协同模式 |

---

## 一、API 设计

### 1.1 `CompositeSchemaAttribute`（契约级约束）

新增特性，标注在**契约接口/基类**上，声明该契约的全局树结构约束：

```csharp
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositeSchemaAttribute : Attribute
{
    /// <summary>Maximum tree depth (root = depth 1). 0 = no limit. Default = 0.</summary>
    public int MaxDepth { get; set; }

    /// <summary>Maximum total node count across all roots. 0 = no limit. Default = 0.</summary>
    public int MaxNodes { get; set; }
}
```

**用法示例**：

```csharp
[CompositeSchema(MaxDepth = 10, MaxNodes = 500)]
public interface IMenuNode : ICompositeNode<IMenuNode> { }
```

### 1.2 `CompositePartAttribute.AllowedChildTypes`（节点级约束）

在现有 `CompositePartAttribute` 上新增可选属性，声明该节点允许的子节点实现类型：

```csharp
// 新增到现有 CompositePartAttribute
public Type[]? AllowedChildTypes { get; set; }
```

**用法示例**：

```csharp
[CompositePart("root", typeof(IMenuNode))]
public partial class MenuRoot : IMenuNode, ICompositeBuildable<IMenuNode> { }

[CompositePart("panel", typeof(IMenuNode), ParentKey = "root", AllowedChildTypes = new[] { typeof(PanelNode), typeof(LeafNode) })]
public partial class PanelNode : IMenuNode, ICompositeBuildable<IMenuNode> { }

[CompositePart("leaf", typeof(IMenuNode), ParentKey = "panel")]
// OK: LeafNode 的父节点 panel 允许 LeafNode
public partial class LeafNode : IMenuNode, ICompositeBuildable<IMenuNode> { }

[CompositePart("section", typeof(IMenuNode), ParentKey = "root", AllowedChildTypes = new[] { typeof(SectionNode) })]
// ERROR: SectionNode 的父节点 root 未在 AllowedChildTypes 中声明 SectionNode
public partial class SectionNode : IMenuNode, ICompositeBuildable<IMenuNode> { }
```

**向后兼容**：`AllowedChildTypes = null`（默认）表示不限制——任何实现契约的类型都可作为子节点，与现有行为一致。

### 1.3 泛型变体同步

`CompositePartAttribute<TContract>` 也新增 `AllowedChildTypes` 属性。`CompositeSchemaAttribute` 不需要泛型变体（它标注在契约类型本身上，无需泛型参数）。

---

## 二、诊断 ID

| ID | 名称 | 严重性 | 归属 | 触发条件 |
|----|------|--------|------|----------|
| DP063 | CompositeTreeMaxDepthExceeded | Warning | SourceGenerators | 树的实际深度超过 `[CompositeSchema(MaxDepth)]` 声明值 |
| DP064 | CompositeChildTypeNotAllowed | Error | SourceGenerators | 子节点的实现类型不在父节点的 `AllowedChildTypes` 集合中 |
| DP065 | CompositeNodeCountExceeded | Warning | SourceGenerators | 契约的总节点数超过 `[CompositeSchema(MaxNodes)]` 声明值 |

### 2.1 诊断消息格式

**DP063**：
- Title: "Composite tree max depth exceeded"
- Message: "Composite tree for contract '{0}' has depth {1}, exceeding the maximum depth of {2} declared by [CompositeSchema]. Consider flattening the tree structure or increasing MaxDepth."
- Description: "Excessively deep composite trees can cause stack overflows in recursive traversal. Review the tree structure or adjust the MaxDepth constraint."

**DP064**：
- Title: "Composite child type not allowed by parent"
- Message: "Composite part '{0}' (type '{1}') is not in the AllowedChildTypes of its parent '{2}' (type '{3}'). Add '{1}' to the parent's AllowedChildTypes or change the ParentKey to a compatible parent."
- Description: "Parent-child type compatibility is enforced at compile time when AllowedChildTypes is specified. Ensure child implementation types are declared in the parent's AllowedChildTypes array."

**DP065**：
- Title: "Composite node count exceeds limit"
- Message: "Composite contract '{0}' has {1} parts, exceeding the maximum of {2} declared by [CompositeSchema]. Consider splitting into multiple contracts or increasing MaxNodes."
- Description: "Excessive node counts may indicate architectural issues or cause performance problems in tree assembly and traversal."

### 2.2 诊断报告位置

- **DP063**：报告在**最深的叶子节点**的 `[CompositePart]` 特性位置（指向造成超限的路径终点）
- **DP064**：报告在**子节点**的 `[CompositePart]` 特性位置（指向不被允许的子节点）
- **DP065**：报告在**契约类型**声明位置（`[CompositeSchema]` 标注的接口/类）

---

## 三、生成器实现

### 3.1 数据流

现有管线不变。新增校验在 `ReportDiagnostics` 阶段执行（与现有 DP010–DP012 同层）：

```
ForAttributeWithMetadataName("CompositePartAttribute")     ← 现有
ForAttributeWithMetadataName("CompositePartAttribute`1")   ← 现有
    → Transform → CompositeRegistration[]                  ← 现有
    → Collect + Combine                                    ← 现有
    → ReportDiagnostics                                     ← 新增 schema 校验
    → Emit                                                 ← 现有
```

### 3.2 深度计算

从 flat parent-key 映射计算树深度（复用现有 `BuildParentMap` 基础设施）：

```csharp
// 伪代码
static int ComputeDepth(string key, IReadOnlyDictionary<string, string?> parentByKey)
{
    var depth = 1;  // root = depth 1
    var children = entries.Where(e => e.ParentKey == key);
    foreach (var child in children)
    {
        depth = Math.Max(depth, 1 + ComputeDepth(child.Key, parentByKey));
    }
    return depth;
}
```

**优化**：使用 memoization 避免重复计算（节点数 N，O(N) 时间 + O(N) 空间）。

**安全**：循环检测已在 DP012 完成，深度计算不会无限递归。

### 3.3 父子类型兼容性校验

```csharp
// 伪代码
foreach (var entry in registrations)
{
    if (entry.ParentKey is null) continue;
    if (!entryByKey.TryGetValue(entry.ParentKey, out var parent)) continue; // DP011 已处理

    var allowedTypes = parent.AllowedChildTypes;
    if (allowedTypes is null || allowedTypes.Length == 0) continue; // 不限制

    if (!allowedTypes.Contains(entry.ImplementationType))
    {
        context.ReportDiagnostic(DP064, entry.Location, ...);
    }
}
```

**注意**：`AllowedChildTypes` 存储的是 `Type[]`，在生成器中需要从特性参数提取 `INamedTypeSymbol` 并比较完全限定名。

### 3.4 节点计数

```csharp
// 伪代码
var schemaAttr = contractType.GetAttributes()
    .FirstOrDefault(a => a.AttributeClass?.Name == "CompositeSchemaAttribute");

if (schemaAttr != null)
{
    var maxNodes = ExtractIntArgument(schemaAttr, "MaxNodes");
    if (maxNodes > 0 && registrations.Count > maxNodes)
    {
        context.ReportDiagnostic(DP065, contractTypeLocation, ...);
    }
}
```

### 3.5 `CompositeSchemaAttribute` 提取

`CompositeSchemaAttribute` 标注在契约接口上，不在 `[CompositePart]` 的目标类上。生成器需要额外提取：

1. 在 Transform 阶段，从 `INamedTypeSymbol contractType` 的 `GetAttributes()` 查找 `CompositeSchemaAttribute`
2. 提取 `MaxDepth` 和 `MaxNodes` 值
3. 存入 `CompositeRegistration` 或单独的 schema 模型

**模型扩展**：在 `CompositeRegistration` 中新增 `int? SchemaMaxDepth` 和 `int? SchemaMaxNodes` 字段（nullable，null = 未标注 `[CompositeSchema]`）。或者使用单独的 `CompositeSchemaInfo` 模型，按契约分组。

选择后者（单独模型），避免每个 `CompositeRegistration` 重复存储：

```csharp
private sealed record CompositeSchemaInfo(
    ContractInfo Contract,
    int? MaxDepth,
    int? MaxNodes,
    LocationInfo? Location);
```

在 Combine 阶段，按 Contract 分组提取 schema 信息，与 registrations 一起传入 ReportDiagnostics。

---

## 四、向后兼容性

| 变更 | 影响 |
|------|------|
| 新增 `CompositeSchemaAttribute` | 无影响——未标注时不触发任何新诊断 |
| `CompositePartAttribute.AllowedChildTypes` 新属性 | 无影响——`null` 默认值表示不限制 |
| 新增 DP063/DP064/DP065 | 无影响——仅在 opt-in 约束被声明时才报告 |

**无破坏性变更**：所有现有 `[CompositePart]` 代码无需修改即可继续工作。

---

## 五、测试计划

### 5.1 生成器单元测试（Verify + 诊断断言）

| 测试 | 验证点 |
|------|--------|
| `ReportsDp063_MaxDepthExceeded` | `[CompositeSchema(MaxDepth = 2)]` + 3 层树 → DP063 Warning |
| `ReportsDp063_NotExceeded_WhenWithinLimit` | `[CompositeSchema(MaxDepth = 5)]` + 3 层树 → 无 DP063 |
| `ReportsDp064_ChildTypeNotAllowed` | 父节点 `AllowedChildTypes = [A, B]`，子节点类型 C → DP064 Error |
| `ReportsDp064_Allowed_WhenTypeInList` | 父节点 `AllowedChildTypes = [A, B]`，子节点类型 B → 无 DP064 |
| `ReportsDp064_NotChecked_WhenAllowedChildTypesNull` | 父节点未设 `AllowedChildTypes` → 无 DP064（任意类型均可） |
| `ReportsDp065_NodeCountExceeded` | `[CompositeSchema(MaxNodes = 3)]` + 5 个节点 → DP065 Warning |
| `ReportsDp065_NotExceeded_WhenWithinLimit` | `[CompositeSchema(MaxNodes = 10)]` + 5 个节点 → 无 DP065 |
| `NoSchemaAttribute_NoNewDiagnostics` | 无 `[CompositeSchema]` + 深树 + 多节点 → 无 DP063/DP065 |
| `MaxDepth_ForestMode_ComputesPerTree` | 多根森林，最深树超过限制 → DP063 报告最深树 |
| `AllowedChildTypes_GenericAttribute` | 泛型 `[CompositePart<T>]` + `AllowedChildTypes` → DP064 正确触发 |

### 5.2 运行时测试

无需新增运行时测试——所有校验在编译期完成，运行时行为不变。

### 5.3 集成测试

新增一个使用 `[CompositeSchema]` + `AllowedChildTypes` 的集成测试项目，验证端到端生成 + 组装 + 遍历流程。

---

## 六、实现范围

| 文件 | 变更 |
|------|------|
| `DesignPatterns/Structural/CompositeSchemaAttribute.cs` | **新增** — 契约级特性 |
| `DesignPatterns/Structural/CompositePartAttribute.cs` | **修改** — 新增 `AllowedChildTypes` 属性 |
| `DesignPatterns/Structural/CompositePartAttribute.TContract.cs` | **修改** — 新增 `AllowedChildTypes` 属性 |
| `DesignPatterns.Diagnostics/DiagnosticIds.cs` | **修改** — 新增 DP063/DP064/DP065 常量 |
| `DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs` | **修改** — 新增 3 个描述符 |
| `DesignPatterns.SourceGenerators/Generators/CompositePartGenerator.cs` | **修改** — 提取 schema、深度计算、类型校验、节点计数 |
| `DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md` | **修改** — 新增 DP063/DP064/DP065 条目 |
| `tests/DesignPatterns.SourceGenerators.Tests/Generators/CompositePartGeneratorTests.cs` | **修改** — 新增 10 个测试 |
| `tests/DesignPatterns.Tests/Structural/CompositeSchemaAttributeTests.cs` | **新增** — 特性构造测试 |
| `docs/Composite.md` | **修改** — 文档补充 schema 校验说明 |
| `docs/ROADMAP.md` | **修改** — 标记完成 |

**PR 边界**：跨 Runtime + Diagnostics + SourceGenerators 三个模块。按 AGENTS.md 规范，应拆分为：
1. **Runtime PR**：`CompositeSchemaAttribute` + `CompositePartAttribute.AllowedChildTypes`
2. **Diagnostics + SourceGenerators PR**：DP063/DP064/DP065 + 生成器校验逻辑 + 测试

或合并为一个 PR（因为特性 + 诊断 + 校验紧密耦合，拆分后第一个 PR 无法独立验证）。选择合并。

---

## 七、探索价值

本 RFC 展示以下编译期 + 运行时协同技术：

1. **从 flat 声明重建树拓扑**：源生成器从 `ParentKey` 字符串引用重建完整树结构，在编译期计算深度、验证类型兼容性——无需运行时反射或动态分析
2. **opt-in 约束的渐进式类型安全**：`AllowedChildTypes` 允许用户逐步收紧类型约束，从「任意契约实现」到「特定实现类型集合」，编译期捕获设计错误
3. **契约级 vs 节点级约束的分层**：`CompositeSchemaAttribute`（契约级全局约束）与 `AllowedChildTypes`（节点级局部约束）正交组合，覆盖不同粒度的校验需求

与 Stateless 等运行时库的对比：本方案将结构校验前移到编译期，运行时零开销；Stateless 等库在运行时动态验证状态机配置，有反射和异常成本。
