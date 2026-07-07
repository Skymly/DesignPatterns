# ADR-007: Composite tree schema validation

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-07 |
| **关联 RFC** | [docs/rfc/archive/CompositeTreeSchemaValidation.md](../rfc/archive/CompositeTreeSchemaValidation.md) |

## 背景

现有 Composite 诊断（DP010–DP015, DP040–DP041）覆盖键唯一性、父键存在性、循环检测、契约实现等基础校验，但不校验树的整体结构形态（最大深度、父子类型兼容性、节点计数）。

## 决策

新增编译期树结构 schema 校验：

- **`[CompositeSchema(MaxDepth, MaxNodes)]`**（契约级约束）
- **`[CompositePart(AllowedChildTypes)]`**（节点级约束）
- **DP063**：最大深度超限（Warning）
- **DP064**：子节点类型不在 `AllowedChildTypes` 集合中（Error）
- **DP065**：节点计数超限（Warning）
- 源生成器阶段从 flat parent-key 映射计算树结构并报告诊断，无需运行时开销

## 后果

**正面**：
- 编译期拦截结构违规，运行时零开销
- `AllowedChildTypes` 提供细粒度父子类型约束
- 与现有 Composite 诊断体系无缝集成

**负面**：
- 生成器需在编译期重建完整树拓扑（深度计算、节点计数）
- `AllowedChildTypes` 使用 `Type[]`，泛型变体需同步维护

## 参考

- [docs/spec/Composite.md](../spec/Composite.md)（待创建）
- [docs/design/Composite.md](../design/Composite.md)（待创建）
- [docs/Composite.md](../Composite.md)（现有模式文档，待迁移）
