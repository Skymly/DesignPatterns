# Research: compile-time synergy depth for hard-gate passers

- **Ticket**: [#252](https://github.com/Skymly/DesignPatterns/issues/252) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Date**: 2026-07-26
- **Branch**: `research/compile-time-synergy-passers`
- **Scope**: Facts and **options** only for the **7 hard-gate passers** locked in [#251](https://github.com/Skymly/DesignPatterns/issues/251). **No Top-3**, no ranking weights, no ROADMAP / map Decisions edits.
- **Question**: For each passer, what is the concrete **compile-time synergy depth** — plausible generator outputs, analyzers/CodeFixes, DI hooks — grounded in this repo’s existing generator patterns and primary / comparable sources?

## Primary sources

| Source | Role |
|--------|------|
| [#251 resolution](https://github.com/Skymly/DesignPatterns/issues/251) | Locked passers + rejection appendix / consolidation |
| Prior family notes on `research/*` under `docs/.Local/research/` | Candidate sketches (#246–#250) |
| [`AGENTS.md`](../../../AGENTS.md) | Shipped domains, diagnostic ownership, DI/Core split |
| [`docs/ROADMAP.md`](../../ROADMAP.md) § naming rules + F3 hard gates | Generated-type naming; admission criteria (context) |
| Design Docs: [Strategy](../../design/Strategy.md), [FactoryRegistry](../../design/FactoryRegistry.md), [ChainOfResponsibility](../../design/ChainOfResponsibility.md), [Decorator](../../design/Decorator.md), [Composite](../../design/Composite.md), [EventAggregator](../../design/EventAggregator.md), [StateTransitionTable](../../design/StateTransitionTable.md) | Attribute → Keys/Registry/Pipeline/Stack/Catalog + diagnostics + `RegisterDi` patterns |
| Generators under `DesignPatterns.SourceGenerators/` (e.g. `RegisterStrategyGenerator`, `DecoratorGenerator`, `HandlerOrderGenerator`, `CompositePartGenerator`, `RegisterEventHandlerGenerator`, `StateTransitionGenerator` + `HierarchyFlattener`) | Incremental `ForAttributeWithMetadataName`, guard validators, DI helper syntax |
| [MediatR README / Contracts](https://github.com/LuckyPennySoftware/MediatR) (`IRequest` / `IRequestHandler`, `IPipelineBehavior`, `IStreamRequest`) | Command Router overlap facts (not rejection) |
| [Polly pipelines / strategies](https://www.pollydocs.org/pipelines/index) | Resilience Pipeline overlap facts (runtime builder vs CT composition) |
| [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) | Channel Pipeline BCL edge primitive |
| [Evans & Fowler, *Specifications*](https://martinfowler.com/apsupp/spec.pdf) | Specification `isSatisfiedBy` + composite Boolean strategies |

## Repo generator pattern cheat-sheet (reuse vocabulary)

| Existing shape | Attribute → glue | Typical diagnostics | DI hook |
|----------------|------------------|---------------------|---------|
| **Keyed registry** (Strategy / Factory) | `[Register*]` → `{Contract}Keys` + `{Contract}Registry` | Duplicate key; contract mismatch; missing attribute Analyzer + CodeFix; literal-key Analyzer (DP025) | `RegisterDi` + `Create(IServiceProvider)` |
| **Ordered pipeline** (Chain) | `[HandlerOrder]` → `{Context}HandlerPipeline` | Duplicate order; missing interface/ctor; unregistered Analyzer (DP024) | `RegisterDi` / `AddHandlerPipeline` |
| **Ordered wrapper stack** (Decorator) | `[Decorator(order)]` → `{Contract}DecoratorStack.Build` + `DecoratorOrder` | Duplicate order; missing `IDecorator<T>`; DI resolvability | `RegisterDi` + `Build(IServiceProvider, core)` |
| **1:N typed fan-out** (Event Aggregator) | `[RegisterEventHandler]` → `{Event}EventHandlerRegistry` | Unregistered / duplicate / contract mismatch | `RegisterDi` + `SubscribeAll(..., IServiceProvider)` |
| **Static graph + flatten** (Composite / State hierarchy) | `[CompositePart]` / `[StateParent]` → catalog / flattened table | Cycles (DP012 / DP056); orphan refs; schema MaxDepth/MaxNodes | `RegisterDi` + resolve-from-provider build |
| **Literal call-site validation** (State DP036, Strategy/Factory DP025) | Analyzer over generated metadata | Unknown edge / unknown key + nearest-key CodeFix | N/A (call-site) |

All sketches below stay within AGENTS hard constraints: **primitives not thick frameworks**; Core **without** MSDI; async first-class; explicit `Try*` failure modes. Diagnostic IDs are **not** allocated here (next free remains **DP067** per AGENTS; DP067–DP071 reserved by ADR-008).

## Consolidation reminders (from #251 — not re-litigated)

- **Command Router** absorbs Request Pipeline Behaviors + Stream Request Router as *capabilities*, not separate domains.
- **Compile-time Resilience Pipeline** absorbs Circuit Breaker Gate + Fallback Provider Chain as *steps*, not separate domains.
- **Specification** absorbs Interpreter + Query Object as fold-ins, not independent domains.

---

## 1. Builder

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `IBuilder<TProduct>` / `BuilderStep` markers; optional manual `BuilderDirector`-less fluent host; `Build()` / `TryBuild(out TProduct)` (explicit failure if required parts missing) |
| Attributes | `[GenerateBuilder<TProduct>]` on product or dedicated builder host; `[BuilderPart(order, Required = true)]` / `[BuilderRequired(nameof(Property))]` on steps or product members |
| Options | Step-order constraints; mutually exclusive parts; default values; `WithX` method generation vs validate-only over hand-written fluent API |

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{Product}Builder` fluent partial with typed `With*` / `Add*` methods from declared parts | Factory/Strategy Keys generation (metadata → members) |
| `{Product}BuilderParts` / order constants | `{Contract}DecoratorOrder` |
| `Build()` / `TryBuild` that fail closed when required parts unset | Composite `BuildRoot` invariants; Strategy `TryGet`/`Get` split |
| Optional `Build(IServiceProvider)` resolving part factories | Decorator/Composite DI `Build(..., IServiceProvider)` |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Required part never set on any `Build()` path (dataflow or attribute completeness) | Error/Warning | Composite schema MaxNodes/MaxDepth (structural CT checks) |
| Duplicate step order / duplicate part key | Error | DP005 / DP016 / DP010 |
| Step type does not implement declared part contract | Error | DP017 / DP013 |
| Call-site Analyzer: `Build()` after incomplete fluent chain when steps are compile-visible | Warning + CodeFix add missing `With*` | DP025 / DP036 style call-site checks |
| CodeFix: add `[BuilderPart]` / generate stub `WithX` | Info+Fix | DP006/DP023 CodeFix |

### DI hooks

- `RegisterDi`: register product as Transient/Scoped factory that runs generated builder with container-resolved parts (Factory Registry default Transient semantics).
- Extension: `AddBuilderFor<TProduct>()` → `IBuilder<TProduct>` / `Func<TProduct>`.
- Autofac twin via existing `RegisterAutofac` / targets pattern.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: required-step completeness, step-order uniqueness, and generated fluent surface from declarations — a hand-rolled fluent Builder cannot prove incomplete `Build()` at compile time without generators/analyzers. Pure runtime only gives `InvalidOperationException` at `Build()`.

---

## 2. Command Router

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `ICommand` / `ICommand<TResult>`; `ICommandHandler<TCommand,TResult>`; `ICommandRouter` with `Send` / `TrySend` / `SendAsync` (1:1, **not** EA 1:N `Publish`) |
| Attributes | `[RegisterCommandHandler]` (open generic or closed); optional `[CommandPipelineBehavior(order)]` as **capability** of this domain (folded from #251) |
| Options | Stream handlers (`IAsyncEnumerable<T>` / MediatR `IStreamRequest` shape) as second generator mode on same router; open-generic handlers |

**Overlap fact (not rejection)**: MediatR `IRequest`/`IRequestHandler` + `IPipelineBehavior` occupy the same problem space; DesignPatterns angle is attribute → frozen router + diagnostics ([EventAggregator.md](../../design/EventAggregator.md) already draws the EA vs MediatR boundary; this domain covers the request half).

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{AssemblyOrMarker}CommandRouter` / per-command `Send` overloads | `{Event}EventHandlerRegistry` + Strategy Registry |
| Command type → handler type map (frozen dictionary on net8.0) | Strategy/Factory `FrozenDictionary` registries |
| Optional `{Command}Pipeline.Build(handler)` ordered behaviors | `{Contract}DecoratorStack.Build(core)` / `{Context}HandlerPipeline` |
| Stream variant: `SendStreamAsync` binding | Dual sync/async Factory / Decorator modes |
| Keys optional only if string/discriminated routing used; default is **CLR type** routing | Contrast: Strategy string keys; EA type routing |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| No handler for command type `T` | Error (generator) or Info (Analyzer on `Send<T>`) | DP044 unregistered handler; DP006 |
| Duplicate handler for same `TCommand` (+ optional `TResult`) | Error | DP045 duplicate RegisterEventHandler; DP003 duplicate strategy key |
| Handler does not implement `ICommandHandler<TCommand,TResult>` | Error | DP046 / DP008 |
| Behavior order collision | Error | DP005 / DP016 |
| Analyzer + CodeFix: implement handler but missing `[RegisterCommandHandler]` | Info+Fix | DP044 / DP024 / DP006 |
| Literal/generic call-site: `Send(typeof(X))` / closed `Send<X>` with no registration | Warning | DP025 / DP036 |
| Open-generic handler arity / constraint mismatch | Error | Guard signature family DP047–DP052 |

### DI hooks

- `RegisterDi`: `TryAdd` each handler + `ICommandRouter` (Transient handlers default, like Event Aggregator handlers).
- `Create(IServiceProvider)` router that resolves handlers per send (avoid captive singleton handlers — reuse DP060–DP062 lifetime Analyzer vocabulary).
- Pipeline behaviors registered ordered, resolved when building per-request pipeline (Decorator DI + Chain `Create(sp)` hybrid).
- Manual: `services.AddCommandRouter(builder => ...)`.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: closed command→handler bijection proofs, missing/duplicate handler diagnostics, and optional generated strongly-typed `Send` overloads. MediatR-style runtime DI scanning discovers handlers late; CT glue can fail the build before a request is ever sent. Runtime overlap remains the `Send`/`TrySend` primitive itself.

---

## 3. Compile-time Resilience Pipeline

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `IResilienceStep<T>` / `ResilienceContext`; `ExecuteAsync(Func<CancellationToken,ValueTask<T>>, ct)`; thin step primitives (retry, timeout, breaker, fallback) as **composable steps**, not a Polly clone |
| Attributes | `[ResiliencePipeline("name")]` host; `[ResilienceStep(order, Kind=Retry|Timeout|…)]` or typed `[RetryStep(order, MaxAttempts=…)]` on step types |
| Options | Named pipelines (registry of frozen graphs); per-operation pipeline attachment; breaker/fallback as **steps** (#251), not domains |

**Overlap fact (not rejection)**: Polly [pipelines](https://www.pollydocs.org/pipelines/index) compose via **runtime** `ResiliencePipelineBuilder`; F3 intent is **compile-time strategy composition** (prior note `docs/.Local/research/resilience-candidates.md` on branch `research/resilience-candidates`).

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{Name}ResiliencePipeline.Build()` / `Instance` frozen onion stack | `{Contract}DecoratorStack.Build(core)`; `{Context}HandlerPipeline.Instance` |
| `{Name}ResilienceStepOrder` int constants | `DecoratorOrder` / Handler orders |
| Optional `{Name}ResiliencePipelineRegistry` for multiple named pipelines | Strategy Keys + Registry |
| Traced execute (`ExecuteTracedAsync`) step statuses | Chain `InvokeTracedAsync` / Strategy `ExecuteTracedAsync` |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Duplicate step order within pipeline | Error | DP016 / DP005 |
| Empty pipeline / missing terminal “execute delegate” slot | Error | Composite single-root invariant |
| Invalid step parameters (retry ≤ 0, timeout ≤ 0) | Error/Warning | Factory pool size DP054/DP055 |
| Incompatible step ordering rules (e.g. documented “timeout outside retry” policy as Warning) | Warning | Composite AllowedChildTypes DP064 (structural policy) |
| Named pipeline key unknown at call site | Warning + nearest-key CodeFix | DP025 |
| Step type missing `IResilienceStep<T>` | Error | DP018 |
| Unregistered step Analyzer when peers exist | Info+Fix | DP024 |

### DI hooks

- `RegisterDi(name)` / `AddResiliencePipeline("name")` registering frozen pipeline as Singleton (stateless composition) while step *state* (breaker counters) is carefully lifetime-scoped — document captive risks (DP062 vocabulary).
- `Build(IServiceProvider)` resolving configurable step dependencies (options monitors).
- Autofac symmetric registration.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: attribute-ordered frozen pipeline graphs, parameter validation, and named-pipeline key diagnostics. Polly already covers rich **runtime** policy behavior; the exploration delta is **schema → generated immutable pipeline + analyzers**, not reimplementing every Polly strategy.

---

## 4. Specification

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `ISpecification<T>` with `IsSatisfiedBy(T)`; `And`/`Or`/`Not` combinators (Evans–Fowler Composite Specification); optional `remainderUnsatisfiedBy` later |
| Attributes | `[RegisterSpecification("key")]` on leaf specs; `[SpecificationComposition("name")]` describing And/Or trees via keys or typeof references |
| Fold-ins (#251) | Interpreter-like expression nodes and Query Object criteria catalogs as **capabilities / alternate backends**, not separate domains |

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{Candidate}SpecificationKeys` + `{Candidate}SpecificationRegistry` | Strategy/Factory Keys + Registry |
| Named composite specs: `{Name}Specification.Instance` built from And/Or/Not of registered leaves | Composite `BuildRoot` / catalog assembly |
| Optional `Expression<Func<T,bool>>` projection helpers for Query Object fold-in | Dual-mode generation (like sync+async Factory) |
| `IsSatisfiedBy` evaluation trampoline (no business logic generation) | Decorator “glue not method bodies” stance |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Duplicate specification key | Error | DP003 / DP010 / DP020 |
| Composition references unknown key / type | Error | DP011 parent key missing |
| Composition graph cycle (And/Or tree) | Error | DP012 / DP056 |
| Spec type does not implement `ISpecification<T>` | Error | DP046 / DP008 |
| Unregistered leaf Analyzer + CodeFix | Info+Fix | DP006 / DP023 / DP044 |
| Literal key at `registry.Get("…")` unknown | Warning+Fix | DP025 |
| Candidate type parameter mismatch in composition | Error | Decorator/Composite contract mismatch |

### DI hooks

- `RegisterDi`: Transient leaf specs + Singleton composed named specs (or all Transient if specs capture scoped services).
- `Create(IServiceProvider)` registry resolving parameterized specs (ctor injection).
- Optional `AddSpecificationsFor<TCandidate>()`.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: named composition graphs validated for missing refs/cycles, Keys + literal-key analyzers, and registry glue. Pure runtime Specification libraries already provide `And`/`Or`/`Not`; CT synergy is **declared catalogs and composition integrity**, not the Boolean algebra itself.

---

## 5. Channel Pipeline

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | Stage interface `IChannelStage<TIn,TOut>`; host that owns `Channel<T>` edges; run/stop with `CancellationToken` |
| Attributes | `[ChannelPipeline("name")]`; `[ChannelStage(order)]` with explicit `In`/`Out` types; optional capacity / `FullMode` metadata |
| BCL | Edges are [`Channel<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1) (documented producer–consumer primitive) |

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{Name}ChannelPipeline.Build()` wiring stage₁ → Channel → stage₂ → … | Composite catalog edge wiring; Handler pipeline assembly |
| `{Name}ChannelStageKeys` / order constants | Strategy Keys + DecoratorOrder |
| Typed channel field holders / pump loops stubs calling user stage methods | State generated table + user actions (glue vs bodies) |
| Health/trace: per-stage processed counts hooks | Chain/EA traced variants |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Stage order gap / duplicate order | Error | DP005 / DP016 |
| Type mismatch on edge (`TOut` of N ≠ `TIn` of N+1) | Error | DP064 AllowedChildTypes; State enum member checks |
| Disconnected stage / unreachable from source | Error/Warning | State orphan / Composite parent missing |
| Pipeline cycle if fan-in/out graphs allowed | Error | DP012 / DP056 |
| Missing stage attribute on `IChannelStage` peer | Info+Fix | DP024 |
| Invalid channel capacity metadata | Error/Warning | DP054 / DP055 |
| DI: stage not registered when using provider build | Error | DP040 |

### DI hooks

- `RegisterDi`: register stages + hosted service / `IChannelPipelineRunner` that builds channels once (Singleton host, Transient stages — document carefully).
- `Build(IServiceProvider)` resolves stages then allocates channels.
- Align with `IHostedService` samples (Sample sketch territory; not implemented here).

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: stage type-edge compatibility, order uniqueness, and missing-edge/disconnected-graph diagnostics. BCL already supplies `Channel<T>` runtime mechanics; CT glue turns an ad-hoc mesh of channels into a **validated staged graph**.

---

## 6. Fork–Join Work Graph

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `IWorkStep` / `IWorkStep<TContext>`; scheduler that runs ready nodes; join on dependency completion (`Task` / `CountdownEvent`) |
| Attributes | `[WorkGraph("name")]`; `[WorkStep("id", DependsOn = new[] { "a", "b" })]` or typeof-based depends |
| Options | Max parallelism; failure policy (fail-fast vs collect); async `ValueTask` steps |

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| `{Name}WorkGraph` with topological order / adjacency list frozen at compile time | State `HierarchyFlattener` + Composite catalog |
| `RunAsync(context, ct)` executing waves of ready nodes | Composite `TraverseParallel*` orchestration (enhancement precedent, new domain graph) |
| `{Name}WorkStepKeys` constants | Strategy/Composite Keys |
| Optional traced run with per-node status | Chain/Strategy traces |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Dependency cycle | Error | DP056 State hierarchy cycle; DP012 Composite parent cycle |
| `DependsOn` unknown id | Error | DP011; State orphan parent DP059 |
| Duplicate step id | Error | DP010 |
| Self-dependency | Error | DP058 self-reference |
| Unreachable / orphan steps (no path from roots) | Warning | State orphan / Composite forest roots |
| Step missing `IWorkStep` / async signature mismatch | Error | DP008; action signature DP037–DP039 |
| Unregistered step Analyzer + CodeFix | Info+Fix | DP024 / DP044 |

### DI hooks

- `RegisterDi`: register step implementations + `IWorkGraph` Singleton (graph shape immutable; step instances lifetime configurable).
- `RunAsync(IServiceProvider, …)` resolving steps per node (Factory-like) vs fixed instances.
- Parallelism options as ctor params on generated host.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: DAG validation (cycles, missing deps, orphans) and frozen topological waves. Runtime TPL/`WhenAll` can express fork–join, but **declared dependency integrity before execution** is the CT exploration surface (same family as State hierarchy flatten + Composite parent cycles).

---

## 7. Proxy

### Proposed attributes / primitives (sketch)

| Piece | Sketch |
|-------|--------|
| Runtime | `IProxy<TSubject>` / access modes: virtual (lazy create), protection (predicate/auth), remote façade stub — **subject access & lifetime**, not cross-cutting layers |
| Attributes | `[GenerateProxy<TSubject>(ProxyKind.Lazy|Protection|…)]` on proxy type; `[ProxySubject(typeof(Real))]`; optional `[ProxyMember]` include/exclude lists |
| Distinction from Decorator | Decorator = ordered wrapper **stack** around a provided core ([Decorator.md](../../design/Decorator.md)); Proxy = **controls access/creation** of a single subject (GoF structural gap per prior note on `research/gof-gap-candidates`) |

### Generator outputs (plausible)

| Output | Analogue in repo |
|--------|------------------|
| Partial proxy class forwarding interface members to subject | Not “method body business logic” — structural forwarding glue (compare: generated registries, not strategy bodies) |
| Lazy subject holder (`Lazy<T>` / double-check) for virtual proxy | Singleton generator lazy patterns |
| Protection gate invoking `CanAccess` / guard method before forward | State/Strategy/Handler `Guard=` nameof validation (DP032/DP047/DP050) |
| Interface-completeness: all interface members forwarded | Composite Visitor coverage intent (DP041 reserved; CS0535 enforces) |

### Diagnostics opportunities

| Opportunity | Severity options | Repo analogue |
|-------------|------------------|---------------|
| Proxy type does not implement subject interface | Error | DP017 |
| Missing subject type / subject not constructible | Error | DP019 / DP014 |
| Guard method missing / non-static / bad signature | Error | DP032/DP034/DP035; DP047–DP052 |
| Member exclude list names unknown members | Error | nameof-style guard checks |
| Analyzer: subject interface implemented without proxy attribute when peers exist | Info | DP044-style |
| Warning when Proxy attribute used but type is also `[Decorator]` (ambiguous intent) | Warning | Lifetime dual-owner style DP067+ vocabulary (conceptual) |
| CodeFix: implement missing interface members / add forwarding stubs | Fix | Existing interface CodeFixes |

### DI hooks

- `RegisterDi`: register proxy as the `TSubject` service (replace real), real as keyed/internal type — mirrors Decorator “register stack as contract” but single hop.
- Lazy proxy: Singleton proxy, Transient subject factory (document captive carefully — DP062).
- `AddProxyFor<TSubject, TProxy>()`.

### Depth note (CT vs pure runtime)

**Uniquely compile-time**: interface forwarding completeness, guard signature checks, and subject-wiring diagnostics. Pure runtime Proxy is a hand-written façade; CT synergy is **generated structural forwarding + access-policy validation**, intentionally **not** Decorator’s multi-layer order stack.

---

## Cross-passer option matrix (no ranking)

| Passer | Closest shipped CT shape | Strongest CT lever | Main ecosystem overlap (fact) |
|--------|--------------------------|--------------------|-------------------------------|
| Builder | Factory Keys + Composite schema | Required-step / incomplete-build proofs | Hand-rolled fluent builders |
| Command Router | Event Aggregator registry + Decorator pipeline | Command↔handler bijection + missing handler | MediatR `IRequest` / behaviors |
| Resilience Pipeline | Decorator stack + Chain order | Frozen attribute pipeline + param checks | Polly `ResiliencePipelineBuilder` |
| Specification | Strategy registry + Composite graph | Composition ref/cycle + Keys | Spec / rules libraries |
| Channel Pipeline | Chain + Composite edges | Stage `TIn`/`TOut` edge typing | BCL `Channel<T>` |
| Fork–Join Work Graph | State hierarchy flatten + Composite cycles | DAG cycle/orphan validation | TPL / custom schedulers |
| Proxy | Decorator (contrast) + Singleton lazy + guards | Forwarding completeness + access guards | Manual / dynamic proxies |

## Non-decisions

- No Top-3, no relative weights inside “compile-time synergy depth” (open on map [#244]).
- No diagnostic ID reservations beyond noting AGENTS next-ID / ADR-008 holds.
- No Design Doc stubs, ROADMAP F3 write-back, or implementation.
- Map [#244] **Decisions so far** intentionally untouched (parent ticket).
)
