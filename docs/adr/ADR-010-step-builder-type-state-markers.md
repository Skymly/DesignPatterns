# ADR-010: Step Builder required completeness via generic type-state markers

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-08-03 |
| **关联 Issue** | [#287](https://github.com/Skymly/DesignPatterns/issues/287)（Spec）、[#288](https://github.com/Skymly/DesignPatterns/issues/288)–[#291](https://github.com/Skymly/DesignPatterns/issues/291) |

## 背景

F3 Top-2 **Builder / Step Builder** 需要在编译期证明「所有必填步骤已应用」后才可调用 `Build()`。可选编码包括：每子集一个接口的阶段图、Analyzer 事后校验调用链、或带 phantom 类型参数的泛型 type-state。须同时限制必填步数量，避免标记组合爆炸，并与注册/装配用的 `*Builder`（如 `FactoryRegistryBuilder`）划清语义边界。

## 决策

Step Builder 的必填完整性**只采用泛型 type-state 标记**，并遵守下列配套约束：

1. **编码**：生成 `{Holder}Builder<…>`，每个**必填**步骤对应一个类型参数，在 `BuilderStepState.NotSet` ↔ `BuilderStepState.Set` 间翻转；仅当全部必填参数均为 `Set` 时暴露可调用的 `Build()`。
2. **上限**：每个 holder 至多 **8** 个必填 `[BuilderStep]`；超出报 **DP078**。可选步骤**不**占用 type 参数，也**不**计入该上限。
3. **非默认路径**：不以「interface-per-subset」阶段图为默认编码；不以 Analyzer 作为必填完整性的**主**门闩（可日后叠加，但不替代 type-state）。
4. **可选 / 互斥 / 偏序**：不进 type-state；schema 级由生成器诊断（DP081–DP082、DP084 等）约束，非法应用顺序在生成的 fluent 方法中以运行时 `InvalidOperationException` 拒绝。
5. **装配**：产品由用户 `[BuilderAssemble]` 方法物化；生成器不发明对象映射。产品类型 = assemble 返回类型。
6. **MVP 非目标**：async `Build`/assemble、MSDI/Autofac、`FromServices` 步进注入、Core 内手写 type-state 孪生、Director 厚基类体系。

## 后果

**正面**：
- 缺必填步时 `Build()` 在调用方编译失败，无需运行时发现
- 标记类型集中在 `BuilderStepState`，生成器与消费方可共享同一 phantom 约定
- 与注册 `*Builder` 的「装配注册表」语义明确分离

**负面**：
- 必填步上限 8 限制超大 schema（有意；可拆 holder 或将多余步标为可选）
- 互斥 / 偏序在 MVP 不以 type-state 擦除，依赖诊断 + 运行时拒绝，调用链上仍可能写出后在运行时报错的非法序列

## 参考

- [docs/design/StepBuilder.md](../design/StepBuilder.md)
- Spec [#287](https://github.com/Skymly/DesignPatterns/issues/287)
- 落地切片：#288（Runtime）→ #289（Diagnostics）→ #290（SourceGenerators）→ #291（Docs）
