# ADR-009: Command Router pipeline uses Chain-like next onion

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-28 |
| **关联 Issue** | [#264](https://github.com/Skymly/DesignPatterns/issues/264)（拆分）、[#275](https://github.com/Skymly/DesignPatterns/issues/275)、[#276](https://github.com/Skymly/DesignPatterns/issues/276)、[#277](https://github.com/Skymly/DesignPatterns/issues/277) |

## 背景

Command Router MVP（1:1 `Send` / 生成器双射）已落地。域内能力「pipeline behaviors」（#264）需要在实现前锁定**一种**洋葱模型：Chain-like `next` 委托，或 Decorator-like 包装 `ICommandHandler`。父 Spec（#256）禁止两种并存。同时需明确是否复用已有 `HandlerPipeline` / `IHandler<TContext>`，以及 void / `TResult` 双路径下的契约形状。

## 决策

Command Router pipeline **只采用 Chain-like `next` 洋葱**，并遵守下列配套约束：

1. **模型**：behavior 接收 command 与 `next`；不调用 `next` 即短路。result 路径用返回值携带短路结果或 `await next` 的结果（MediatR 形）。**不**提供 Decorator-like `Decorate(inner)` / 嵌套 `ICommandHandler` 包装作为并列模型。
2. **契约**：双接口，与 handler 同构——`ICommandPipelineBehavior<TCommand>` 与 `ICommandPipelineBehavior<TCommand, TResult>`。
3. **作用域**：按命令类型**封闭**注册（特性绑定 `TCommand` + `order`）；本批不做开放泛型全局横切。
4. **类型边界**：使用 Command Router **专用** behavior / delegate / builder API；**不**把命令塞进 `HandlerPipeline<TContext>` / `IHandler<TContext>`（语义对齐 Chain，类型不耦合）。
5. **特性**：`[CommandPipelineBehavior<TCommand>(order)]` 与非泛型 `(order, typeof(TCommand))`；`AllowMultiple = true`；数值越小越先 inbound（最外层）。
6. **手动路径**：`CommandRouterBuilder` 一等支持 behavior；`Build` 时冻结洋葱。
7. **本批非目标**：behavior 的 DI / Autofac；未注册 behavior Analyzer；开放泛型 behavior。

诊断（重复 order、孤儿 behavior、契约不匹配）与模块切片见 #275–#277 与更新后的 #264。

## 后果

**正面**：
- 与已落地 Chain onion、MediatR `IPipelineBehavior` 对照清晰，短路与 outbound 自然
- 保持 Command Router 域语言（void/`TResult`、冻结映射），避免与 Decorator「同一服务契约叠层」混淆
- 实现切片可按 Runtime → Diagnostics → SourceGenerators → Docs 分 PR

**负面**：
- 横切多命令需 `AllowMultiple` 多次标注或拆类，不如开放泛型省事（有意推迟）
- 不复用 `HandlerPipeline` 会多一套薄运行时类型（换取边界清晰）

## 参考

- [docs/design/CommandRouter.md](../design/CommandRouter.md)
- [docs/design/ChainOfResponsibility.md](../design/ChainOfResponsibility.md)（onion / `next` 先例）
- Spec [#256](https://github.com/Skymly/DesignPatterns/issues/256)；能力票 [#264](https://github.com/Skymly/DesignPatterns/issues/264) / [#275](https://github.com/Skymly/DesignPatterns/issues/275)–[#277](https://github.com/Skymly/DesignPatterns/issues/277)
