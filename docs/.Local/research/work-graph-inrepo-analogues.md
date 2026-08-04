# Research: in-repo analogues for Fork–Join Work Graph Spec writing

- **Ticket**: [#301](https://github.com/Skymly/DesignPatterns/issues/301) (map [#300](https://github.com/Skymly/DesignPatterns/issues/300))
- **Date**: 2026-08-04
- **Branch**: `research/work-graph-inrepo-analogues`
- **Scope**: Facts and **options** only — which already-shipped surfaces are the best analogues for (1) compile-time DAG / cycle / orphan validation and (2) runtime wave or parallel orchestration. **Do not** lock Spec API shapes, diagnostic severities, or `DP###` reservations.
- **Question**: Which already-shipped DesignPatterns surfaces are the best analogues for Fork–Join Work Graph MVP Spec writing?

## Primary sources

| Source | Role |
|--------|------|
| [#300](https://github.com/Skymly/DesignPatterns/issues/300) charting notes | Locked MVP preferences (context for *what* analogues must cover); not Spec text |
| Prior note [`concurrency-candidates.md`](concurrency-candidates.md) on `origin/research/concurrency-candidates` | Named Fork–Join Work Graph; pointed at State hierarchy cycle checks |
| Prior note [`compile-time-synergy-passers.md`](compile-time-synergy-passers.md) on `origin/research/compile-time-synergy-passers` §6 + cross-passer matrix | Explicit CT/runtime analogue table for Work Graph (still options, not Spec) |
| [ADR-005](../../adr/ADR-005-state-transition-table.md), [StateTransitionTable.md](../../design/StateTransitionTable.md) | Dual path + hierarchy flatten + DP056–DP059 |
| [ADR-006](../../adr/ADR-006-composite-parallel-traversal.md), [Composite.md](../../design/Composite.md) parallel sections | BFS same-layer / DFS child-parallel traversal; MaxDop; AggregateException; TFM split |
| [ADR-007](../../adr/ADR-007-composite-tree-schema-validation.md), Composite DP010–DP015 / DP063–DP065 | Parent-key graph + schema MaxDepth/MaxNodes |
| [StepBuilder.md](../../design/StepBuilder.md) | Named-step `After`/`Before` partial-order cycle / unknown-ref (DP082 / DP084) |
| [CommandRouter.md](../../design/CommandRouter.md) DP076 | Orphan-registration Error (behavior without terminal handler) |
| [ChainOfResponsibility.md](../../design/ChainOfResponsibility.md), [EventAggregator.md](../../design/EventAggregator.md) | Contrast: ordered sequential pipelines / sequential fan-out — **not** wave orchestration |
| Generators: `CompositePartGenerator`, `StateTransitionValidator` + `HierarchyFlattener`, `GenerateBuilderGenerator` (cycle DFS) | Implementation facts for cycle / orphan / graph rebuild |
| Runtime: `DesignPatterns/Structural/CompositeTraverser.cs`, `CompositeTraversalOptions.cs` | Parallel orchestration knobs and contracts |
| `DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md` | Shipped diagnostic severities (authoritative over AGENTS summary where they diverge) |
| [`docs/ROADMAP.md`](../../ROADMAP.md) F3 #3 | Work Graph one-liner: attribute DAG; generator validates cycles/orphans; topological wave orchestration |

## Charting reminder (context only — not Spec)

Map [#300] already locks (for later Spec writing): shared `TContext`; string step ids + `DependsOn`; async-only MVP; fail-fast; dual path (runtime builder **and** attribute generator); same-wave concurrency with documented unsynchronized-write ban; DI out of MVP. This note does **not** re-litigate those; it only maps **shipped** analogues Spec authors can cite.

---

## 1. Compile-time DAG / cycle / orphan validation

### Best primary analogues (ranked by closeness of shape)

| Rank | Shipped surface | Graph shape | Cycle | Missing / unknown edge | Orphan / unreachable | Identity style vs Work Graph charting |
|------|-----------------|-------------|-------|------------------------|----------------------|---------------------------------------|
| **A** | **State hierarchy** — `[StateParent]` + `StateTransitionValidator` + `HierarchyFlattener` | Parent map (tree / forest of states); flattened at CT | **DP056** Error (parent-chain cycle string `"A -> B -> … -> A"`) | **DP057** Error (invalid enum member) | **DP059** **Info** (parent with no children and no outgoing transitions) — descriptor + Unshipped; Design Doc / AGENTS tables sometimes say Error (doc drift) | Enum member names, not free string ids; still the cycle/orphan **vocabulary** prior research cited |
| **B** | **Composite parent-key catalog** — `[CompositePart(ParentKey=…)]` + `CompositePartGenerator` | Flat key→parent map rebuilt into tree/forest at CT | **DP012** Error (`ParticipatesInCycle` walk) | **DP011** Error (ParentKey not in registered keys) | Multi-root forest is **valid** (`BuildForest`); single-root enforced at **runtime** by `BuildRoot` (`CompositeAssemblyException`) — not a CT “orphan step” diagnostic | **String keys** (`{Contract}CompositeKeys`) — closest identity match to charted `{Name}WorkStepKeys` |
| **C** | **Step Builder partial order** — `[BuilderStep(After=, Before=)]` | Directed constraints among named steps | **DP082** Error (constraint cycle) | **DP084** Error (After/Before → unknown step) | No dedicated “unreachable step” diagnostic in MVP | **String step names** + `nameof` — closest to charted `DependsOn = string[]` edge labels |

### Secondary CT analogues (useful citation, weaker graph fit)

| Surface | What it models | Limit as Work Graph analogue |
|---------|----------------|------------------------------|
| **DP058** State self-parent | Self-edge rejection | Narrow; maps to “self-DependsOn” *option*, not full DAG |
| **DP010** Composite duplicate key / **DP083** Builder duplicate step | Duplicate node id | Registry uniqueness, not reachability |
| **DP063–DP065** Composite schema (MaxDepth Warning / AllowedChildTypes Error / MaxNodes Warning) | Structural policy over rebuilt topology | Policy knobs, not dependency readiness |
| **DP076** Command Router orphan behavior | Declared node without required peer (behavior w/o terminal handler) | Registration orphan, not DAG reachability from roots |
| **DP031** State “never used as `from`” Info | Soft unused-node hint | Terminal-state hint, not dependency orphan |

### Implementation facts Spec writers can mirror (options, not locks)

1. **Cycle detection style**
   - Composite: walk parent map; report each key that participates (`CompositePartGenerator.ReportCycles` / `ParticipatesInCycle`).
   - State: DFS parent map; emit human-readable cycle path (`StateTransitionValidator.DetectCycle`).
   - Step Builder: adjacency from After/Before edges; DFS reports one cycle edge (`GenerateBuilderGenerator.HasCycle`).
2. **Unknown reference**
   - Composite DP011 / Builder DP084 / (State uses enum membership DP057 rather than free-string lookup).
3. **Orphan severity precedent is mixed in-repo**
   - DP059 orphan parent = **Info** (descriptors).
   - DP076 orphan behavior = **Error**.
   - Composite unknown parent DP011 = **Error**; “extra roots” are legal for forest assembly.
   - Map #300 still lists orphan severity as **Not yet specified** — cite these as **options**, do not pick here.
4. **Frozen graph output analogue**
   - Prior synergy note: `{Name}WorkGraph` topo / adjacency ≈ State `HierarchyFlattener` + Composite catalog assembly (compile-time proof, runtime zero re-validate of shape when using generated path).
5. **Dual path already shipped** (assembly, not DAG-specific): State `TransitionTableBuilder` + generator; Composite `CompositeTreeBuilder` + catalog; Command `CommandRouterBuilder` + generator — precedent for map #300’s Core builder **and** attribute generator without choosing APIs here.

### Prior research cross-links (still options)

From `compile-time-synergy-passers.md` §6 (not Spec):

| Work Graph diagnostic opportunity (sketch) | Repo analogue cited there |
|--------------------------------------------|---------------------------|
| Dependency cycle | DP056; DP012 |
| `DependsOn` unknown id | DP011; DP059 |
| Duplicate step id | DP010 |
| Self-dependency | DP058 |
| Unreachable / orphan steps | State orphan / Composite forest roots |
| Contract / async signature mismatch | DP008; DP037–DP039 family |

From `concurrency-candidates.md` longlist item 4: generator “validates acyclicity (**cf. State hierarchy cycle checks**)”.

---

## 2. Runtime wave or parallel orchestration patterns

### Best primary analogue

| Surface | Why it is the closest shipped parallel orchestrator | Facts Spec writers should note |
|---------|-----------------------------------------------------|--------------------------------|
| **Composite `TraverseParallel*` / `TraverseForestParallel*`** ([ADR-006](../../adr/ADR-006-composite-parallel-traversal.md), `CompositeTraverser`) | Only shipped runtime that schedules **same-layer concurrent work** with explicit dop limits | **BFS same-layer parallel** ≈ topological **wave** of independent nodes; **DFS child-parallel** is a second strategy; `MaxDegreeOfParallelism` (`null` → `Environment.ProcessorCount`); `MaxParallelDepth` fallback to serial; async `ValueTask` + `CancellationToken`; net8.0 `Parallel.ForEachAsync` vs netstandard2.0 `SemaphoreSlim` + `Task.WhenAll`; visitation order **non-deterministic** |

### Contract / failure-mode facts (options for Spec — do not lock)

| Topic | Composite parallel precedent | Map #300 preference (context) |
|-------|------------------------------|-------------------------------|
| Shared mutable state | Documented **user responsibility**; no thread-safe collector provided | Overlapping unsynchronized writes to `TContext` forbidden; library does **not** isolate/merge |
| Failure aggregation | Visitor exceptions → `ConcurrentQueue` → **`AggregateException`** after work (collect-then-throw) | MVP **fail-fast** (cancel peers; throw) — **different** from Composite; Spec may cite EA `StopOnError` / Chain short-circuit as *fail-fast* vocabulary instead |
| Forest roots | Roots **serial**; parallelism **inside** each root | Multi-root work graphs would need an explicit MVP choice (not locked here) |
| Dop knob | Shipped `MaxDegreeOfParallelism` | Map lists whether MVP exposes dop as **Not yet specified** |

### Weaker / contrast surfaces (avoid over-citing as “wave” analogues)

| Surface | Runtime shape | Why weaker for Work Graph waves |
|---------|---------------|----------------------------------|
| **Chain `HandlerPipeline`** | Ordered `next` middleware; optional `InvokeTracedAsync` | Strictly **sequential**; shared `TContext` is a good *context* analogue, not parallelism |
| **Command Router + pipeline onion** ([ADR-009](../../adr/ADR-009-command-router-pipeline-onion.md)) | 1:1 command→handler; ordered behaviors | Sequential onion; no fork–join DAG |
| **Event Aggregator `PublishAsync`** | Snapshot then **sequential** `await` handlers; optional `AggregateException` collect mode | Fan-out is **1:N typed pub/sub**, not dependency waves; Design Doc states non-parallel |
| **State `IStateMachine` / transition table** | Single-threaded transition; hierarchy is CT flatten, not runtime parallel | Graph at CT; execution is not multi-node scheduling |
| **Step Builder generated fluent** | Type-state construction; no runtime scheduler | Partial-order is CT/schema only |

### Dual-path runtime builder option (orchestration host, not parallel algorithm)

Shipped domains commonly expose a **manual builder** that validates at `Build()` plus a **generated frozen host**. For Work Graph, that is an assembly/hosting analogue (`WorkGraphBuilder` / `IWorkGraph` in charting notes) — parallel **wave scheduling** itself still points at Composite `TraverseParallel*` mechanics more than at Chain/Command builders.

---

## 3. Option matrix for Spec authors (no ranking of Spec choices)

| Concern | Strongest in-repo analogue | Alternate analogue | Explicit non-analogue |
|---------|----------------------------|--------------------|-----------------------|
| Cycle in dependency graph | State DP056 **or** Composite DP012 **or** Builder DP082 | — | Chain order collision (DP005) — order ints, not edges |
| Unknown dependency id | Composite DP011 **or** Builder DP084 | State DP057 (enum membership) | DP025 literal key (call-site registry) |
| Orphan / unreachable node | DP059 Info **or** DP076 Error **or** “forest roots are OK” (Composite) | DP031 Info unused state | — |
| Duplicate step id | Composite DP010 / Builder DP083 | Strategy DP003 | — |
| Self-edge | State DP058 | — | — |
| Frozen topo / adjacency emit | HierarchyFlattener + Composite catalog | Strategy/Factory FrozenDictionary registries | — |
| Same-wave parallel run | Composite BFS `TraverseParallel*` | — | EA sequential Publish; Chain pipeline |
| Max parallelism knob | `CompositeTraversalOptions.MaxDegreeOfParallelism` | — | — |
| Shared context + “you sync it” contract | Composite parallel thread-safety docs; Chain `TContext` | — | — |
| Fail-fast vs aggregate | EA `StopOnError` vs Composite/`ContinueOnError` AggregateException | — | Do not assume AggregateException is MVP default |
| Dual path (manual + generated) | State / Composite / Command Router | Factory/Strategy registries | — |
| String Keys type | Composite / Strategy / Factory `*Keys` | Builder step names | State enum members |

---

## Non-decisions

- No Spec API (`IWorkStep`, attribute ctor shapes, `RunAsync` result type).
- No diagnostic severity matrix or `DP###` range reservation.
- No choice whether `MaxDegreeOfParallelism` is MVP (still open on map #300).
- No ROADMAP / map #300 body edits; no Work Graph implementation.
- No re-ranking of F3 Top-3.

## Suggested follow-ups (outside this ticket)

- Spec draft (#300 destination) can cite **§1 A–C** for CT validation vocabulary and **§2 Composite parallel** for wave orchestration, calling out fail-fast vs AggregateException as an intentional MVP delta.
- Optional doc hygiene: align AGENTS / State Design Doc DP059 severity wording with `DesignPatternsDiagnosticDescriptors` (**Info**).
)
