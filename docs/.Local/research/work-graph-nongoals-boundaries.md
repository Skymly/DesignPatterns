# Research: Work Graph Non-goals boundaries vs Channel Pipeline, Composite parallel traversal, and TPL/Dataflow

- **Ticket**: [#302](https://github.com/Skymly/DesignPatterns/issues/302) (map [#300](https://github.com/Skymly/DesignPatterns/issues/300))
- **Date**: 2026-08-04
- **Branch**: `research/work-graph-nongoals-boundaries-6a73`
- **Scope**: Boundary **facts and citation targets** for the future Fork–Join Work Graph MVP Spec **Non-goals** section. **No** Spec prose finalization. **No** implementation. **No** new F3 admissions.
- **Question**: For the Fork–Join Work Graph MVP Spec Non-goals section, what precise boundaries should be cited against (1) ROADMAP watch-list Channel Pipeline, (2) Composite parallel traversal (shipped enhancement, ADR-006), and (3) external TPL / Dataflow-style graphs — so the Spec can state what this domain is *not* without inventing new admissions?

## Locked map preference (constraint; not re-opened)

Map [#300](https://github.com/Skymly/DesignPatterns/issues/300) already locks:

- Data model: **shared `TContext`**; edges express **dependency/readiness only** — **not** typed `TIn`/`TOut` payload edges (**Channel Pipeline stays on the watch list**).
- Out of scope (map body): admitting/specifying Channel Pipeline (and other watch/rejection items); typed payload edges; sync `Execute`; aggregate/continue failure; context snapshot/merge framework.

This note only supplies **citeable primary-source wording** for those Non-goals lines.

## Primary sources

| Source | Role |
|--------|------|
| [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 Top-3 / 观望 / 出局附录 / 长期探索候选 | Official placement of Work Graph vs Channel Pipeline vs Composite parallel |
| [ADR-006](../../adr/ADR-006-composite-parallel-traversal.md) | Accepted decision for Composite parallel traversal |
| [`docs/design/Composite.md`](../../design/Composite.md) | Shipped Composite parallel API, structure focus, thread-safety contract |
| [`docs/.Local/research/concurrency-candidates.md`](concurrency-candidates.md) on `origin/research/concurrency-candidates` | Family longlist: Channel Pipeline vs Fork–Join Work Graph sketches; Composite parallel **excluded** as enhancement |
| [`docs/.Local/research/compile-time-synergy-passers.md`](compile-time-synergy-passers.md) on `origin/research/compile-time-synergy-passers` | CT sketches + cross-passer matrix for Channel Pipeline vs Work Graph |
| Wayfinder [#251](https://github.com/Skymly/DesignPatterns/issues/251) resolution | Hard-gate passers include both Channel Pipeline and Work Graph; rejection appendix (e.g. Phased Barrier, Actor Mailbox) |
| Wayfinder [#253](https://github.com/Skymly/DesignPatterns/issues/253) / ROADMAP writeback | Top-3 admits Work Graph #3; Channel Pipeline remains 观望 |
| Map [#300](https://github.com/Skymly/DesignPatterns/issues/300) | Locked MVP preferences + Out of scope (cited, not edited) |
| Peer Spec [#256](https://github.com/Skymly/DesignPatterns/issues/256) Out of Scope | Precedent Non-goals style: “Resilience / Specification / Channel Pipeline / Proxy (watch list)” |
| [Channels (.NET)](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) | Official BCL producer/consumer `Channel<T>` model |
| [Task Parallel Library (TPL)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) | Official TPL purpose (parallelism/concurrency APIs) |
| [Dataflow (Task Parallel Library)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) | Official TPL Dataflow message-passing / pipeline-network model |

---

## 1. vs ROADMAP watch-list **Channel Pipeline**

### What the repo already admits / places

| Fact | Citation |
|------|----------|
| Channel Pipeline is **观望** (hard-gate passer, **not** Top-3): one-line description = **阶段 `TIn`/`TOut` 边类型校验 + BCL `Channel<T>` 接线** | [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 观望 |
| Fork–Join Work Graph is Top-3 #3: **属性声明的工作 DAG；生成器校验环 / 孤儿依赖并生成拓扑波次编排** | same § Top-3 |
| Both passed hard gates as **separate** passers; Channel was **not** folded into Work Graph | [#251](https://github.com/Skymly/DesignPatterns/issues/251) resolution (passers 5 + 6) |
| Family research split: Channel = typed stage in/out + `Channel<T>` edges; Work Graph = `[WorkStep(DependsOn=…)]` static DAG + join orchestration | `concurrency-candidates.md` longlist items 1 vs 4 |
| CT sketches: Channel strongest lever = **stage `TIn`/`TOut` edge typing**; Work Graph = **DAG cycle/orphan validation** | `compile-time-synergy-passers.md` §§ 5–6 + cross-passer matrix |
| Map #300 already forbids typed payload edges and keeps Channel on the watch list | [#300](https://github.com/Skymly/DesignPatterns/issues/300) Notes / Out of scope |
| Peer Spec Non-goals precedent: list Channel Pipeline as **watch list**, do not specify it | [#256](https://github.com/Skymly/DesignPatterns/issues/256) Out of Scope |

### External BCL wording (for “what Channel Pipeline would wrap”, not an admission)

[Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels): `System.Threading.Channels` provides synchronization structures for **passing data between producers and consumers asynchronously**; producer/consumer model = data through a **FIFO** queue; typed `Channel<T>` / `ChannelWriter<T>` / `ChannelReader<T>`.

### Boundary recommendation (citeable contrast — no new admission)

For Spec Non-goals, cite that Work Graph MVP is **not** Channel Pipeline by contrasting **already-recorded** shapes:

1. **Edge semantics**: Work Graph edges = **dependency/readiness** (`DependsOn`) over shared `TContext` ([#300](https://github.com/Skymly/DesignPatterns/issues/300)); Channel Pipeline edges = **typed stage payload** `TIn`/`TOut` + BCL `Channel<T>` wiring ([ROADMAP](../../ROADMAP.md) 观望; research sketches).
2. **Domain status**: Channel Pipeline remains **观望 / unspecified** in this Spec (same Non-goals pattern as [#256](https://github.com/Skymly/DesignPatterns/issues/256)); do **not** absorb Channel into Work Graph or invent Channel APIs here.
3. **CT exploration surface**: Work Graph = declared DAG integrity + topological waves ([ROADMAP](../../ROADMAP.md) Top-3 row; `compile-time-synergy-passers.md` §6); Channel = stage order + **type-edge compatibility** on channels (`compile-time-synergy-passers.md` §5) — different passer, still on watch list.

**Do not invent**: capacity/`FullMode` Channel options, `IChannelStage<TIn,TOut>`, or any claim that Work Graph “replaces” Channels.

---

## 2. vs **Composite parallel traversal** (shipped enhancement, ADR-006)

### What the repo already admits / places

| Fact | Citation |
|------|----------|
| Composite parallel traversal is a **long-term / shipped enhancement of Composite**, not an F3 new-domain shortlist item: `TraverseParallel*` + `MaxDegreeOfParallelism` + `MaxParallelDepth` — Phase 1 implemented | [`docs/ROADMAP.md`](../../ROADMAP.md) 长期探索候选 row (struck as done) |
| ADR accepted: BFS same-level parallel; DFS child parallel recursion; `AggregateException`; TFM `#if` (`Parallel.ForEachAsync` vs `SemaphoreSlim`+`Task.WhenAll`); **thread-safety = user responsibility + doc contract** (no thread-safe collector) | [ADR-006](../../adr/ADR-006-composite-parallel-traversal.md) |
| Design Doc: Composite = **tree** part/whole + unified traversal; parallel visit order **non-deterministic**; forest roots **serial**, parallelism inside each root’s subtree | [`docs/design/Composite.md`](../../design/Composite.md) §§ 并行遍历 / 与生态的边界 / 已知局限 |
| Composite vs Chain vs Strategy table: Composite = tree traversal; Chain = linear ordered pipeline; Strategy = flat key dispatch — **no substitution** among them | same Design Doc § 与生态的边界 |
| Concurrency family research **excluded** Composite parallel from new-domain longlist: “Enhancement of **Composite** (Phase 1 shipped; ADR-006)” | `concurrency-candidates.md` EXCLUDED table |
| Work Graph CT sketch may *mention* `TraverseParallel*` only as **orchestration precedent**, while calling out a **new domain graph** | `compile-time-synergy-passers.md` §6 generator outputs row |

### Boundary recommendation (citeable contrast — no new admission)

For Spec Non-goals, cite that Work Graph is **not** Composite parallel traversal / not an extension of Composite:

1. **Domain placement**: Composite parallel = **enhancement of shipped Composite** (ADR-006 / ROADMAP long-term); Work Graph = **separate F3 Top-3 new domain** ([ROADMAP](../../ROADMAP.md) Top-3 #3). Do not fold Work Graph into `CompositeTraverser` or treat it as Composite Phase 2.
2. **Structure being scheduled**: Composite parallel walks an **already-assembled part/whole tree** (parent→children via catalog / builder); Work Graph schedules a **declared work-step DAG** (`DependsOn` / topological waves) ([ROADMAP](../../ROADMAP.md) Top-3 wording; Composite Design Doc tree focus).
3. **Visitor vs steps**: Composite API is `TraverseParallel*(root, visitor, options)` over nodes; Work Graph admitted shape is attribute/runtime **work steps** with identity keys + readiness edges ([#300](https://github.com/Skymly/DesignPatterns/issues/300); research sketches) — not a Composite visitor.
4. **Failure-mode precedent differs**: ADR-006 / Design Doc use **`AggregateException`** after collecting visitor failures; map [#300](https://github.com/Skymly/DesignPatterns/issues/300) locks Work Graph MVP **fail-fast** (cancel in-flight peers; throw) with aggregate/continue as Phase 2+. Non-goals should not imply ADR-006’s aggregate policy is Work Graph MVP.
5. **Reusable vocabulary only**: ADR-006’s “user responsibility + documentation contract” for concurrent access to shared state is a **citation precedent** for documenting `TContext` write races ([#300](https://github.com/Skymly/DesignPatterns/issues/300) same-wave note) — **not** an admission that Work Graph is Composite parallel.

**Do not invent**: claiming Work Graph supersedes `TraverseParallel*`, or that Composite trees become Work Graphs.

---

## 3. vs external **TPL / Dataflow**-style graphs

### Repo stance on ecosystem overlap (already recorded)

| Fact | Citation |
|------|----------|
| F3 admission: overlap with ecosystem libraries (MediatR / Polly / `Microsoft.Extensions.*` / Stateless, etc.) is **not** a rejection reason; value is lightweight primitive + **compile-time** exploration | [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 intro; [`AGENTS.md`](../../../AGENTS.md) project purpose |
| Work Graph CT depth note: runtime TPL / `WhenAll` can express fork–join, but **declared dependency integrity before execution** is the CT surface | `compile-time-synergy-passers.md` §6 Depth note + matrix (“Main ecosystem overlap: TPL / custom schedulers”) |
| Phased Barrier Coordination **rejected** as thin BCL `Barrier`/`CountdownEvent` façade | [#251](https://github.com/Skymly/DesignPatterns/issues/251) rejection appendix; ROADMAP 出局附录 |
| Actor Mailbox **rejected** (hard to keep lightweight primitive) | same |
| Map #300 Out of scope already excludes typed payload edges and context merge frameworks | [#300](https://github.com/Skymly/DesignPatterns/issues/300) |

### Official MS wording (boundary against *wrapping* these libraries)

| Library | Citeable shape | Source |
|---------|----------------|--------|
| **TPL** | Public types/APIs in `System.Threading` / `System.Threading.Tasks` to simplify adding **parallelism and concurrency**; handles partitioning, ThreadPool scheduling, cancellation, state management, low-level details | [Task Parallel Library (TPL)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) |
| **TPL Dataflow** | *TPL Dataflow Library* promotes **actor-based** programming via **in-process message passing** for coarse-grained dataflow and **pipelining**; useful when operations must **communicate asynchronously** or process data **as it becomes available** | [Dataflow (TPL)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) |
| **Dataflow blocks** | Source / target / propagator blocks with **`TOutput` / `TInput` / `TInput,TOutput`**; connect into **pipelines** (linear) or **networks** (graphs) via `LinkTo`; sources **propagate data to targets as data becomes available** | same |
| **Dataflow buffering / shared-data claim** | Explicit control over **how data is buffered and moves**; declare handling when data is available and **dependencies between data**; runtime managing data dependencies can **avoid synchronizing access to shared data** | same |
| **Channels** (related external primitive Channel Pipeline would use) | Async **producer/consumer** FIFO typed data handoff — not a static work DAG | [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) |

### Boundary recommendation (citeable contrast — no new admission)

For Spec Non-goals, cite that Work Graph MVP is **not** a TPL Dataflow (or Channels) façade / replacement:

1. **Not Dataflow message networks**: Dataflow centers on **typed message blocks** (`ITargetBlock<TInput>`, `ISourceBlock<TOutput>`, `IPropagatorBlock<TInput,TOutput>`), `LinkTo` pipelines/networks, and **buffered message passing**. Work Graph MVP (map [#300](https://github.com/Skymly/DesignPatterns/issues/300)) uses **shared `TContext` + dependency edges only** — no per-edge typed payloads, no `LinkTo`-style dynamic block graphs, no Dataflow buffering model.
2. **Not “ship a scheduler framework”**: TPL’s purpose is general parallelism/concurrency infrastructure (scheduling, partitioning, ThreadPool). Work Graph’s admitted exploration value is **attribute-declared DAG + compile-time cycle/orphan validation + topological wave orchestration** ([ROADMAP](../../ROADMAP.md) Top-3; `compile-time-synergy-passers.md` §6) — overlapping *runtime mechanics* with `Task`/`WhenAll` is allowed by F3, but the Spec should not claim to re-implement or wrap `System.Threading.Tasks.Dataflow`.
3. **Contrast with rejected thin façades**: [#251](https://github.com/Skymly/DesignPatterns/issues/251) rejected Phased Barrier as thin BCL sync façade and Actor Mailbox as too heavy. Non-goals should keep Work Graph on the **declared DAG + CT proof** side of that line — not Barrier/CountdownEvent wrappers, not actor mailboxes, not Dataflow block catalogs.
4. **Shared-context honesty**: Dataflow docs emphasize avoiding shared-data sync by passing messages. Map [#300](https://github.com/Skymly/DesignPatterns/issues/300) explicitly chooses shared `TContext` and documents that the library does **not** isolate/merge context. Non-goals can cite that Work Graph is **not** adopting Dataflow’s message-isolation model (without inventing a merge framework — already out of scope on the map).

**Do not invent**: `ActionBlock`/`TransformBlock` APIs, Dataflow `LinkTo` generation, or a claim of TPL feature parity. Overlap with `Task`/`ValueTask`/`CancellationToken` as **implementation substrate** remains compatible with existing F3 “overlap OK” language — that is *not* an admission of a Dataflow domain.

---

## Cross-boundary gist (for Spec authors)

Use only these contrasts in Non-goals; all are grounded in sources above:

| Neighbor | Work Graph is *not*… | Cite |
|----------|----------------------|------|
| **Channel Pipeline** (观望) | Typed `TIn`/`TOut` stage edges + BCL `Channel<T>` wiring; do not admit/specify that domain | ROADMAP 观望; #300; #256 Out of Scope pattern; Channels docs for BCL shape |
| **Composite parallel traversal** | Parallel **tree visitor** enhancement of Composite (`TraverseParallel*`, ADR-006) | ADR-006; Composite Design Doc; ROADMAP long-term; concurrency EXCLUDED table |
| **TPL Dataflow / Channels graphs** | Message-passing block networks / producer–consumer FIFO graphs with typed buffered payloads | MS Dataflow + Channels + TPL docs; CT depth note (DAG proof vs bare `WhenAll`) |

## Non-decisions / out of scope for this note

- Final Spec Non-goals prose, severity matrices, API shapes, diagnostic IDs.
- Implementation of Work Graph runtime / generator / analyzer / Samples.
- Editing map [#300](https://github.com/Skymly/DesignPatterns/issues/300) body.
- Promoting Channel Pipeline, changing ADR-006, or new ROADMAP admissions.
)
