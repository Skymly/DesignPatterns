# Research: resilience / fault-tolerance pattern candidates

- **Ticket**: [#248](https://github.com/Skymly/DesignPatterns/issues/248) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Question**: For the resilience / fault-tolerance family, which **new** pattern domain candidates fit DesignPatterns’ primitive + compile-time-glue model (including compile-time strategy composition vs Polly-shaped libraries)?
- **Scope**: longlist only; **new domains only**; **no admission ranking**.
- **Branch**: `research/resilience-candidates`
- **Date**: 2026-07-26

## Primary sources

| Source | Role |
|--------|------|
| [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 | Candidate pool names **Resilience primitive（探索编译期策略组合）**; overlap with Polly is not a rejection reason; hard gates listed but **not applied** here |
| [Polly resilience strategies](https://www.pollydocs.org/strategies/index) | Official built-in strategies + reactive/proactive split |
| [Polly resilience pipelines](https://www.pollydocs.org/pipelines/index) | Official composition model: runtime `ResiliencePipelineBuilder` / named registry |
| [`docs/design/Strategy.md`](../../design/Strategy.md) | Shipped **select-by-key** composition reference (`[RegisterStrategy]` → Keys + Registry) |
| [`docs/design/Decorator.md`](../../design/Decorator.md) | Shipped **ordered wrapper stack** composition reference (`[Decorator(order)]` → `{Contract}DecoratorStack.Build(core)`) |

## Composition references (DesignPatterns)

| Shipped domain | Composition shape | Compile-time glue |
|----------------|-------------------|-------------------|
| Strategy | One-of-N algorithm by key (`TryGet` / `Get`) | `[RegisterStrategy]` → Keys + Registry + guards / DP006 / DP025 / DI |
| Decorator | Ordered outer→inner wrappers around a core | `[Decorator(order)]` → `DecoratorStack.Build(core)` + `DecoratorOrder` constants / DP016 |

These are the library’s proven “primitive + glue” shapes for **selection** vs **ordered stacking**. Resilience candidates below are framed as **new domains** that reuse those shapes (or a close variant), not as enhancements to Strategy/Decorator themselves (enhancements are out of map [#244] shortlist scope).

## Polly overlap facts (not rejection)

From Polly docs ([strategies](https://www.pollydocs.org/strategies/index), [pipelines](https://www.pollydocs.org/pipelines/index)):

| Polly surface | Shape |
|---------------|--------|
| Built-in strategies | Reactive: Retry, Circuit Breaker, Fallback, Hedging. Proactive: Timeout, Rate Limiter |
| Composition | Fluent **runtime** `ResiliencePipelineBuilder` / `ResiliencePipelineBuilder<T>` — strategies cannot run alone; they run through a pipeline |
| Naming / DI | Named pipelines via `ResiliencePipelineRegistry` / `AddResiliencePipeline` (resolve by name at runtime) |

**Factual overlap with a DesignPatterns resilience exploration**: same *problem space* (transient faults, fail-fast, degrade, deadlines). **Factual difference sought by ROADMAP F3**: explore **compile-time strategy composition** (attribute/schema → generated frozen pipeline / registry / diagnostics), not a Polly-shaped fluent runtime builder library.

ROADMAP F3 already registers the umbrella name “Resilience primitive”; this ticket **splits** that family into concrete **new domain** longlist entries (still unranked).

## Longlist (new domains only)

No ranking, hard-gate pass/fail, or admission.

1. **Compile-time Resilience Pipeline** — Attribute-/schema-ordered resilience steps (retry, timeout, breaker, …) that generate a frozen `Build`/`Execute` stack in the **DecoratorStack** shape, contrasting Polly’s runtime `ResiliencePipelineBuilder` composition.
2. **Named Resilience Policy Registry** — Strategy-registry-shaped map of named policy graphs (`[RegisterResiliencePolicy]` or equivalent → Keys + Registry + literal-key diagnostics / DI), overlapping Polly’s `ResiliencePipelineRegistry` role while exploring compile-time key/glue.
3. **Circuit Breaker Gate** — Lightweight open / half-open / closed gate primitive plus compile-time wiring of trip thresholds and predicates (stateful fail-fast domain; Polly Circuit Breaker strategy as runtime-capability overlap, not a model to copy).
4. **Fallback Provider Chain** — Ordered degrade-path providers composed like Handler/Decorator order for graceful alternatives when the primary call fails (Polly Fallback strategy overlap; DesignPatterns angle is ordered CT composition of providers, not pipeline builder options).

## Explicitly not listed (near-duplicates / other families)

| Idea | Why omitted from this longlist |
|------|--------------------------------|
| “Add retry/timeout as Decorator layers” on existing Decorator | Existing-pattern enhancement (F2+ / map [#244] out of shortlist) |
| “Select retry vs fail-fast via Strategy keys” only | Existing-pattern enhancement of Strategy, not a new domain |
| Rate Limiter / Bulkhead Isolation | Polly lists Rate Limiter as proactive resilience; concurrency/coordination family ticket [#249] is the better home for isolation/throughput gates |
| Hedging as a standalone domain | Plausible later; truncated to 4 with clearer CT composition analogues (pipeline / registry / breaker / fallback) |
| Umbrella “Resilience primitive” as a single domain name | Kept as F3 pool wording; longlist prefers separable domain shapes for later gates |

## Non-decisions

- No hard-gate evaluation (ticket [#251]).
- No compile-time synergy ranking (ticket [#252]).
- No Top-3 / ROADMAP F3 write-back (tickets [#253]–[#254]).
- Map [#244] intentionally untouched.
