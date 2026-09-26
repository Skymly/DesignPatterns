# ADR-011: Step Builder async assemble exits through BuildAsync

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-26 |
| **关联 Issue** | [#324](https://github.com/Skymly/DesignPatterns/issues/324)（Spec）、[#325](https://github.com/Skymly/DesignPatterns/issues/325)–[#327](https://github.com/Skymly/DesignPatterns/issues/327) |

## 背景

[ADR-010](ADR-010-step-builder-type-state-markers.md) 把必填完备性锁在泛型 type-state 上，并把 async `Build` / assemble 列为 MVP 非目标。产品物化若需要 I/O，作者只能把 `[BuilderAssemble]` 保持同步，或绕过生成的 `{Holder}Builder`，从而失去同一套必填门闩。

需要在不改 type-state 证明模型、不新增运行时类型、不新开诊断 ID 的前提下，给装配出口一条 async 路径。

## 决策

Step Builder 的 **async 只发生在 assemble 出口**。ADR-010 仍是 type-state 与必填上限的决策，**不**被本 ADR 取代。本 ADR 解除 ADR-010 第 6 点里的 async `Build` / assemble 非目标，并只在 async 出口上收窄「产品类型 = assemble 返回类型」的表述。

1. **互斥出口**：每个 holder 仍恰好一个 `[BuilderAssemble]`。返回同步产品 `T` 时只生成 `Build()`；返回 BCL `Task<T>` 或 `ValueTask<T>` 时只生成 `BuildAsync`，**不**再生成 `Build()`。不允许同一 holder 同时暴露两个出口。其它返回类型（含自定义 awaitable）不是 async 出口，按同步产品类型生成 `Build()`；裸 `Task` / `ValueTask` 见第 5 点。
2. **返回类型跟随装配**：`BuildAsync` 的返回类型与 assemble 相同（`Task<T>` 或 `ValueTask<T>`）。不把 `ValueTask<T>` 包成 `Task<T>`，也不把任务拆成同步 `T`。
3. **产品类型**：async 时产品类型是内层 `T`，任务只是传输。同步时产品类型仍是 assemble 返回的 `T`（ADR-010）。生成器不发明映射，直接 `return` assemble 的任务或值任务，不额外制造 async 状态机。
4. **取消**：`BuildAsync` 始终带 `CancellationToken cancellationToken = default`。assemble 可以不接收 token，或接收至多一个 `CancellationToken`；若存在，生成器按该参数在 assemble 签名中的位置原样传入（按类型识别，不要求参数名为 `cancellationToken`）。该参数**不**参与步骤名绑定。同步 `Build()` 不接受 `CancellationToken`；同步 assemble 上的 `CancellationToken` 仍按普通参数做步名绑定（绑不到则 **DP080**）。
5. **非法契约仍用 DP086**（不新开诊断 ID）：裸 `Task` / `ValueTask`、void、重复 `[BuilderAssemble]`、多于一个 `CancellationToken`（同步或 async），以及既有的不可访问实例装配。DP078–DP085 语义不变。
6. **type-state 不变**：必填步仍是 `NotSet` → `Set`；`BuildAsync` 与 `Build()` 一样，只在全部必填类型参数为 `Set` 时作为扩展方法存在。可选步、互斥组、`After` / `Before` 不因 async 放宽。必填上限仍为 8。
7. **仍非目标**：async `[BuilderStep]` / 可等待 fluent 链；async assemble 上的 sync-over-async `Build()`；MSDI / Autofac / `FromServices`；步骤参数校验诊断；互斥的 type-state 擦除；新 Analyzer / CodeFix；新运行时属性或类型。

## 后果

**正面**：

- I/O 装配可以留在生成 builder 的 type-state 门闩之后，不必绕过 `{Holder}Builder`
- 同步 schema 仍只有 `Build()`，既有调用点不变
- 不新增诊断 ID、不新增 Core 类型；DP086 文案覆盖合法与非法契约

**负面**：

- 同一 holder 不能同时提供同步与异步出口；需要两种出口时必须拆 holder
- 自定义 awaitable 不会变成 `BuildAsync`（有意：出口形状保持可判定）
- assemble 忽略取消时，`BuildAsync` 仍接受 token 但不转发；调用方不能从 `BuildAsync` 签名看出 token 是否被观察

## 参考

- [ADR-010](ADR-010-step-builder-type-state-markers.md) — type-state 与必填上限；本 ADR 不取代它
- [docs/design/StepBuilder.md](../design/StepBuilder.md)
- Spec [#324](https://github.com/Skymly/DesignPatterns/issues/324)
- 落地切片：#325（Diagnostics，DP086 文案）→ #326（SourceGenerators）→ #327（Docs）
