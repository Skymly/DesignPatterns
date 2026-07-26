# Research: Concurrency / coordination pattern candidates

- **Issue**: [#249](https://github.com/Skymly/DesignPatterns/issues/249) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Date**: 2026-07-26
- **Branch**: `research/concurrency-candidates`
- **Scope**: Longlist only — **new** pattern domains; **no** admission gates, ranking, or ROADMAP / map edits
- **Question**: For the concurrency / coordination family, which **new** pattern domain candidates fit DesignPatterns’ primitive + compile-time-glue model?

## Primary sources

| Source | Role |
|--------|------|
| [`AGENTS.md`](../../../AGENTS.md) — “已实现的模式（摘要）” | Shipped domains (runtime + generator) |
| [`docs/design/README.md`](../../design/README.md) | Design Doc index for shipped domains |
| [`docs/ROADMAP.md`](../../ROADMAP.md) §§ F2+, F3, 长期探索候选 | Enhancement backlog that must be **excluded** from this longlist |
| [ADR-006](../../adr/ADR-006-composite-parallel-traversal.md) | Composite parallel traversal is an **enhancement** of Composite (shipped Phase 1) |
| [Overview of synchronization primitives](https://learn.microsoft.com/en-us/dotnet/standard/threading/overview-of-synchronization-primitives) | BCL signaling / barrier / countdown / semaphore shapes |
| [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) / [`Channel<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1) | BCL async producer–consumer channel primitive |

Sibling family tickets (boundary only; not expanded here): messaging / command routing [#247](https://github.com/Skymly/DesignPatterns/issues/247), resilience / fault tolerance [#248](https://github.com/Skymly/DesignPatterns/issues/248).

## Shipped baseline (diff against)

Shipped **pattern domains** today ([`AGENTS.md`](../../../AGENTS.md), Design Docs): Singleton, Factory Registry, Strategy, Chain, Composite, Decorator, Event Aggregator, State Transition Table.

**Concurrency already present only as enhancements inside shipped domains** (not new domains):

- Composite parallel traversal (`TraverseParallel*` / `MaxDegreeOfParallelism`) — ADR-006 / ROADMAP F3 long-term (Phase 1 shipped).
- Async paths on Strategy / Factory / Decorator / EventAggregator / Chain — F2 / F2+ enhancements of those domains.
- Factory pooling (`IPooledFactoryRegistry`, DP053–DP055) — creational enhancement, not a concurrency coordination domain.

There is **no** shipped domain whose primary shape is multi-party coordination, actor mailboxes, channel stage graphs, or static work DAGs.

## EXCLUDED (ROADMAP long-term / F2+ enhancements — not candidates)

Per map #244 / ticket #249: enhancements to already-shipped domains stay on F2+ / long-term backlog and must **not** appear as candidates.

| Item (from ROADMAP) | Why excluded |
|---------------------|--------------|
| Composite 并行遍历 | Enhancement of **Composite** (Phase 1 shipped; ADR-006). |
| Composite 懒加载 | Enhancement of **Composite** (`LazyChildren` / `AssembleAsync`). |
| Composite 树 schema / DI / Visitor | Enhancement of **Composite** (DP040–DP041, DP063–DP065). |
| Decorator 组合 / 嵌套 / DI / async | Enhancement of **Decorator**. |
| State hierarchy / entry-exit / Autofac / traces | Enhancement of **State**. |
| Strategy / Chain guards & traces; EventAggregator publish tracing / error modes | Enhancements of **Strategy** / **Chain** / **Event Aggregator**. |
| Factory async + pooling | Enhancement of **Factory Registry**. |
| Singleton lifecycle diagnostics; DI keyed / health / lifetime | Enhancements of **Singleton** / DI cross-cuts — not concurrency domains. |
| F3 pool: Observer / pub-sub 扩展, Command 路由, Resilience primitive, Builder, ObjectPool | Wrong family for this ticket (messaging / resilience / GoF / pooling) or named elsewhere (#246–#248). |

Also **not** listed as candidates here (thin BCL wrappers or other-family): bare `SemaphoreSlim` / `Monitor` façades; bulkhead / rate-limit / retry (→ resilience #248); MediatR-style command bus (→ messaging #247).

## Longlist (2–4 new domain candidates)

No gates applied. Each must be imaginable as a **lightweight runtime primitive** plus **attribute → generated registries / graphs / diagnostics / DI hooks**, distinct from Chain (same-context ordered handlers) and Event Aggregator (typed pub/sub).

1. **Channel Pipeline (staged producer–consumer)** — Attribute-registered stages with typed in/out contracts; generator wires `Channel<T>` edges + stage Keys / missing-edge diagnostics / optional DI host (BCL: [`Channel<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1)).
2. **Phased Barrier Coordination** — Named phases + `[BarrierParticipant]` (or equivalent) participant registration; generator emits participant counts / phase Keys over BCL [`Barrier`](https://learn.microsoft.com/en-us/dotnet/standard/threading/overview-of-synchronization-primitives) / [`CountdownEvent`](https://learn.microsoft.com/en-us/dotnet/standard/threading/overview-of-synchronization-primitives) with count/mismatch diagnostics.
3. **Actor Mailbox** — Addressable actors with typed message handlers and a single-consumer mailbox queue; compile-time routing table + unknown-message / missing-handler analyzers (sequential-per-actor coordination; **not** Event Aggregator pub/sub).
4. **Fork–Join Work Graph** — `[WorkStep(DependsOn=…)]` (or equivalent) static DAG of work nodes; generator validates acyclicity (cf. State hierarchy cycle checks) and emits join orchestration over `Task` / `CountdownEvent`.

## Non-goals

- Admission gates, ranking, Top-3 shortlist, or `docs/ROADMAP.md` F3 / map #244 updates (later tickets on map #244).
- Implementation of runtime, generators, analyzers, or samples.
