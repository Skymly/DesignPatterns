# Research: Shipped pattern domains & F3 candidate pool

**Ticket:** [#245](https://github.com/Skymly/DesignPatterns/issues/245) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
**Branch:** `research/shipped-domains-f3-inventory`
**Scope:** Facts only — no ranking, no admission advice.
**Primary sources:** `AGENTS.md`, `docs/ROADMAP.md`, `docs/design/*`, runtime under `DesignPatterns/`, `DesignPatterns.SourceGenerators/`, `DesignPatterns.Analyzers/`.

---

## 1. Shipped pattern domains (runtime + generator / analyzer presence)

Sources: [AGENTS.md §已实现的模式（摘要）](../../AGENTS.md), [docs/design/README.md](../design/README.md), generators under `DesignPatterns.SourceGenerators/Generators/`, analyzers under `DesignPatterns.Analyzers/`.

| Domain | One-line shape | Runtime (paths / types) | Generator | Analyzer presence (IDs) |
|--------|----------------|-------------------------|-----------|-------------------------|
| **Singleton** | Compile-time `Lazy<T>` singleton for a `partial` class via attribute. | `DesignPatterns/Creational/GenerateSingletonAttribute.cs` | `GenerateSingletonGenerator` | Generator DP001–DP002; Analyzer DP062 (`CaptiveDependencyAnalyzer`), DP066 (`FactoryDelegateCaptiveDependency` / singleton factory delegates), DP067+ (`SingletonLifecycleAnalyzer` per ADR-008 / `DiagnosticIds.cs`) |
| **Factory Registry** | Keyed factory registry: each `Create` runs a factory delegate → new product. | `DesignPatterns/Creational/` — `IFactoryRegistry`, `IAsyncFactoryRegistry`, `IPooledFactoryRegistry`, `[RegisterFactory]` | `RegisterFactoryGenerator` | Generator DP020–DP022, DP053–DP055; Analyzer DP023 (`UnregisteredFactoryAnalyzer`), DP025 (`UnknownRegistryKeyAnalyzer`), DP033 (`CrossAssemblyRegistryKeyAnalyzer`) |
| **Strategy** | Keyed strategy registry + optional sync/async marker interfaces; no routing engine. | `DesignPatterns/Behavioral/` — `IStrategyRegistry`, `IStrategy` / `IAsyncStrategy`, `[RegisterStrategy]` | `RegisterStrategyGenerator` | Generator DP003–DP004, DP007, DP047–DP049; Analyzer DP006 (`UnregisteredStrategyAnalyzer`), DP025, DP033 |
| **Chain** | Middleware-style handler pipeline (`IHandler<T>` + `HandlerPipeline`) with order attributes. | `DesignPatterns/Behavioral/` — `IHandler<T>`, `HandlerPipeline`, `[HandlerOrder]` | `HandlerOrderGenerator` | Generator DP005, DP008–DP009, DP050–DP052; Analyzer DP024 (`UnregisteredHandlerAnalyzer`) |
| **Composite** | Tree `ICompositeNode` + catalog assembly / traversal primitives; `[CompositePart]` catalog. | `DesignPatterns/Structural/` — `ICompositeNode`, `CompositeTreeBuilder`, `[CompositePart]`, `[CompositeSchema]` | `CompositePartGenerator` | Generator DP010–DP015, DP040–DP041, DP063–DP065 |
| **Decorator** | Composable wrapper stack around a core service; `[Decorator]` compile-time order. | `DesignPatterns/Structural/` — `IDecorator` / `IAsyncDecorator`, `DecoratorStackBuilder`, `[Decorator]` | `DecoratorGenerator` | Generator DP016–DP019, DP042–DP043 |
| **Event Aggregator** | In-process typed pub/sub (`Subscribe` / `PublishAsync`) without a full message bus. | `DesignPatterns/Behavioral/` — `IEventAggregator`, `IEventHandler<T>`, `[RegisterEventHandler]` | `RegisterEventHandlerGenerator` | Generator DP045–DP046; Analyzer DP044 (`UnregisteredEventHandlerAnalyzer`) |
| **State (transition table)** | `(state, trigger) → next` table primitive + optional hierarchy / `IStateMachine` wrapper. | `DesignPatterns/Behavioral/` — `ITransitionTable`, `IStateMachine`, `IStateHierarchy`, `[StateMachine]` / `[Transition]` / `[StateParent]` | `StateTransitionGenerator` | Generator DP026–DP032, DP034–DP035, DP037–DP039, DP056–DP059; Analyzer DP036 (`StateTransitionLiteralEdgeAnalyzer`) |

**Also listed under AGENTS “已实现的模式” but not a pattern domain Design Doc:**

| Item | One-line shape | Notes |
|------|----------------|-------|
| **DI Health Checks** | `AddDesignPatternsHealthChecks` + `IHealthCheck` registration resolvability checks. | Runtime extension only (no dedicated generator). Cross-cutting DI analyzers DP060–DP061 live in `LifetimeMismatchAnalyzer`. No entry in `docs/design/README.md`. |

**Design Doc index** (`docs/design/README.md`): Strategy, Chain of Responsibility, Composite, Factory Registry, Decorator, Event Aggregator, State Transition Table. **No** dedicated Singleton Design Doc in that index.

---

## 2. Current F3 candidate-pool names (ROADMAP)

Source: [docs/ROADMAP.md § F3 — 候选新模式](../ROADMAP.md) (candidate pool paragraph; “仅登记，未排期”).

| Candidate name (as nominated) | One-line description (from ROADMAP wording) |
|-------------------------------|---------------------------------------------|
| **Observer / 轻量 pub-sub 扩展** | Observer / lightweight pub-sub extension (candidate pool entry). |
| **Builder 生成器** | Builder generator (candidate pool entry). |
| **ObjectPool** | Explore source-generated pooling strategies. |
| **Resilience primitive** | Explore compile-time strategy composition. |
| **Command 路由** | Explore differentiation points vs MediatR. |

ROADMAP also states: scope includes GoF but is not limited to GoF (concurrency / reactive / functional candidates welcome); **do not implement until admission criteria are passed**.

---

## 3. Explicit “enhancement not new domain” — must **not** enter this map’s shortlist

These are registered under **F2+** or **长期探索候选** as enhancements of **already-shipped** domains (or cross-cutting DI), not as F3 new-pattern-domain candidates. Source: [docs/ROADMAP.md](../ROADMAP.md) §§ F2+, F3 long-term exploration table.

### From F2+ — 现有模式增强

| Item | Why it is enhancement-not-new-domain |
|------|--------------------------------------|
| State entry/exit actions + `IStateMachine` wrapper | Extends **State** (`[Transition(OnEnter/OnExit)]`, DP037–DP039). |
| Composite DI + Visitor generation | Extends **Composite** (`RegisterDi` / `BuildRoot(IServiceProvider)`, DP040–DP041). |
| Decorator DI + Async variant | Extends **Decorator** (`IAsyncDecorator`, DP042–DP043). |
| EventAggregator source generator + auto-subscribe | Extends **Event Aggregator** (`[RegisterEventHandler]`, DP044–DP046). |
| Strategy / Chain guard predicates | Extends **Strategy** and **Chain** (DP047–DP052). |
| Factory async + pooling | Extends **Factory Registry** (`IAsyncFactoryRegistry` / pool, DP053–DP055). |
| State Autofac support | Extends **State** DI/Autofac parity. |
| Generated-code quality (`#nullable`, `[GeneratedCode]`, XML docs) | Cross-generator hygiene, not a domain. |
| Strategy execution tracing | Extends **Strategy** observability. |
| Chain exception observability | Extends **Chain** observability. |
| EventAggregator publish tracing | Extends **Event Aggregator** observability. |

### From F3 section “长期探索候选” (enhancements of shipped domains / DI)

| Candidate | Why it is enhancement-not-new-domain |
|-----------|--------------------------------------|
| State 层级状态机 | Hierarchy for **State** (ROADMAP: implemented v3.1–v3.4; DP056–DP059). |
| Composite 并行遍历 | Parallel traversal for **Composite** (Phase 1 implemented; ADR-006). |
| Composite 懒加载 | Lazy children / `AssembleAsync` for **Composite** (still exploration). |
| Composite 树 schema 校验 | Schema constraints for **Composite** (implemented; DP063–DP065; ADR-007). |
| Decorator 组合 / 嵌套 | `Compose(otherStack)` for **Decorator**. |
| MSDI keyed services（.NET 8+） | DI registration enhancement (keyed), not a pattern domain. |
| DI 健康检查 + 生命周期校验 | Cross-cutting DI / health (partially shipped; DP060–DP062 family). |
| Singleton 生命周期诊断 | Diagnostics for **Singleton** / DI capture (ADR-008; DP066–DP071). |

**Note:** F2 structural items (Composite forest, Decorator conditional order, Handler short-circuit observability) are likewise enhancements of shipped domains; they are completed under F2 in the same ROADMAP file and are also outside an F3 *new-domain* shortlist.

---

## Source checklist

| Source | Used for |
|--------|----------|
| `AGENTS.md` — 已实现的模式 / 诊断 ID 表 | Domain names, attribute/API, generator names, diagnostic ownership |
| `docs/ROADMAP.md` — F2+, F3, 长期探索 | F3 pool wording; enhancement-not-new-domain inventory |
| `docs/design/README.md` + per-domain Design Docs | One-line shapes for domains with Design Docs |
| `DesignPatterns/{Behavioral,Creational,Structural}/` | Runtime surface confirmation |
| `DesignPatterns.SourceGenerators/Generators/*Generator.cs` | Generator class names |
| `DesignPatterns.Analyzers/*.cs` | Analyzer class presence |
| `DesignPatterns.Diagnostics/DiagnosticIds.cs` | Diagnostic ID ranges cited above |
