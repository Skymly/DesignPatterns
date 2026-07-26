# Research: GoF gap candidates for new pattern domains

- **Issue**: [#246](https://github.com/Skymly/DesignPatterns/issues/246) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Date**: 2026-07-26
- **Scope**: Longlist only — **no** admission gates, ranking, or ROADMAP edits
- **Question**: Across GoF creational / structural / behavioral patterns **not** already shipped as DesignPatterns domains, which are plausible **new** domain candidates for a primitive + compile-time-glue library?

## Primary sources

| Source | Role |
|--------|------|
| [Gamma et al., *Design Patterns* (GoF)](https://en.wikipedia.org/wiki/Design_Patterns) catalog (23 patterns) | Exhaustive creational / structural / behavioral set to diff against |
| [`AGENTS.md`](../../../AGENTS.md) — “已实现的模式（摘要）” | Shipped domain inventory (runtime + generator) |
| [`docs/design/README.md`](../../design/README.md) | Design Doc index for shipped domains |
| [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 | Existing F3 candidate-pool names (context only; not admission) |
| [`docs/design/FactoryRegistry.md`](../../design/FactoryRegistry.md) § 已知局限 | Explicit “不做抽象工厂族” |
| [`docs/design/EventAggregator.md`](../../design/EventAggregator.md) § 参考 | Positions Event Aggregator as Observer / Mediator adjacent |
| [`docs/design/Composite.md`](../../design/Composite.md) (Visitor / DP041) | Visitor is Composite enhancement, not a standalone domain |

## Shipped domains vs GoF (diff basis)

Shipped as **pattern domains** today ([`AGENTS.md`](../../../AGENTS.md), Design Docs):

| Shipped domain | Closest GoF pattern(s) |
|----------------|------------------------|
| Singleton | Singleton |
| Factory Registry | Factory Method / registry variant (not Abstract Factory) |
| Strategy | Strategy |
| Chain of Responsibility | Chain of Responsibility |
| Composite | Composite |
| Decorator | Decorator |
| Event Aggregator | Observer / Mediator *variant* (not a separate Observer or Mediator domain) |
| State Transition Table | State |

**Not** treated as standalone shipped domains for this gap list:

- **Visitor** — generated as Composite DI/visitor glue (DP040–DP041), enhancement of Composite ([`ROADMAP.md`](../../ROADMAP.md) F2+), not its own domain.
- **Observer / Mediator** as named domains — only Event Aggregator is shipped; F3’s “Observer / 轻量 pub-sub 扩展” reads as pub-sub *extension* of that surface, not a net-new GoF domain here.
- Factory Registry **builders**, Decorator **stacks**, etc. — assembly helpers inside existing domains.

## GoF patterns still outside shipped domains

| Category | Not shipped as a domain |
|----------|-------------------------|
| Creational | Abstract Factory, Builder, Prototype |
| Structural | Adapter, Bridge, Facade, Flyweight, Proxy |
| Behavioral | Command, Interpreter, Iterator, Mediator, Memento, Observer*, Template Method, Visitor* |

\*Near-duplicate or enhancement of shipped surfaces (see above); omitted from the longlist below.

**Skipped as pure documentation / weak compile-time imaginability** (per ticket preference): Facade (API façade narrative), Iterator (covered by `IEnumerable` / language iterators), Template Method (typically thick inheritance skeletons).

## Longlist (2–4 candidates)

No gates applied. Prefer imaginable compile-time glue (attributes → generated registries / routers / diagnostics / DI hooks).

1. **Builder** — Attribute-declared build steps / required fields → generated fluent `*Builder` + incomplete-build / step-order diagnostics (and optional DI `RegisterDi` for configured products). Already named in F3 pool ([`ROADMAP.md`](../../ROADMAP.md) § F3).
2. **Command** — `[RegisterCommandHandler]` (or equivalent) on handlers → generated typed command router / dispatcher + missing-handler / duplicate-handler analyzer (+ DI registration). Already named in F3 pool ([`ROADMAP.md`](../../ROADMAP.md) § F3); messaging-family ticket #247 may deepen this later.
3. **Proxy** — Access / lazy / virtual proxy primitives distinct from Decorator stacks → attribute-driven proxy façade generation with interface-completeness checks and optional DI wiring (control *access* and lifetime of the subject, not layered cross-cutting wrappers).
4. **Abstract Factory** — Product-*family* registries (related factories resolved together) as a **new** domain — Factory Registry explicitly declines abstract factory families ([`FactoryRegistry.md`](../../design/FactoryRegistry.md) § 已知局限); compile-time shape: family keys + per-product factories + cross-product completeness diagnostics / DI family registration.

## Explicitly not listed (rationale)

| Pattern | Why omitted from this longlist |
|---------|--------------------------------|
| Observer / Mediator | Near-duplicate of shipped Event Aggregator; map decision on near-duplicates still open (#244) |
| Visitor | Composite enhancement (F2+), not a new domain |
| Flyweight / Prototype / Adapter / Bridge / Interpreter / Memento | Plausible later; truncated to 4 with clearer CT + documented gap / F3 overlap |
| Enhancements to Composite / Decorator / State / etc. | Out of scope per #246 / #244 |

## Non-goals

- Admission gates, ranking, Top-3 shortlist, or `docs/ROADMAP.md` F3 updates (later tickets on map #244).
- Implementation of runtime, generators, analyzers, or samples.
