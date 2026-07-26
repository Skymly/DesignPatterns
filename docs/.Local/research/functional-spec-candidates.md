# Research: functional / specification-style pattern candidates

- **Ticket**: [#250](https://github.com/Skymly/DesignPatterns/issues/250) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Date**: 2026-07-26
- **Branch**: `research/functional-spec-candidates`
- **Scope**: Longlist only — **new** pattern domains; **no** admission gates, ranking, or ROADMAP / map edits
- **Question**: For the functional / specification-style family, which **new** pattern domain candidates fit DesignPatterns’ primitive + compile-time-glue model (e.g. Specification and nearby forms)?

## Primary sources

| Source | Role |
|--------|------|
| [Evans & Fowler, *Specifications*](https://martinfowler.com/apsupp/spec.pdf) | Authoritative Specification pattern: `isSatisfiedBy`; uses (selection, validation, construction-to-order); Hard Coded / Parameterized / Composite strategies; Subsumption; Partially Satisfied (`remainderUnsatisfiedBy`); Composite Spec via Interpreter |
| [Wikipedia — Specification pattern](https://en.wikipedia.org/wiki/Specification_pattern) | Composite Specification + Boolean recombination (`And` / `Or` / `Not`); DDD usage notes; cites Evans & Fowler |
| [`docs/design/README.md`](../../design/README.md) | Shipped Design Doc index (no Specification / Interpreter / Query Object domain) |
| [`AGENTS.md`](../../../AGENTS.md) — “已实现的模式（摘要）” | Shipped domains: Singleton, Factory Registry, Strategy, Chain, Composite, Decorator, Event Aggregator, State |
| [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 | Candidate pool lists **Builder 生成器** (creational); functional patterns welcome; overlap with ecosystem libs is not a rejection reason |

## Shipped baseline (diff against)

No shipped DesignPatterns domain is a boolean business-predicate / criteria / expression-evaluation domain. Closest existing pieces are **orthogonal**:

- **Strategy** — key → interchangeable algorithm (not boolean composition of rules over a candidate).
- **Composite** — tree of *domain nodes*, not And/Or/Not over predicates.
- **State guards** — edge predicates inside the State domain (enhancement surface), not a standalone Specification domain.
- **Chain** — ordered handlers transforming a context, not admit/reject via composable specs.

Therefore classic Specification and its nearby forms are **new domains** relative to the current catalog.

## Builder vs Specification (F3 pool distinction)

| | **GoF Builder** (F3 pool: “Builder 生成器”) | **Specification** (this family) |
|--|---------------------------------------------|----------------------------------|
| Intent | Stepwise **construction** of a product (`Build()` / director) | Separate **matching criteria** from the candidate (`isSatisfiedBy`) |
| Primary API shape | Fluent setters / parts → product | Predicate objects + Boolean combinators |
| Fowler link | Unrelated creational pattern | Paper’s **construction-to-order** is a *use* of Specification (describe requirements so a candidate can be built to satisfy them)—**not** GoF Builder |

**Do not** treat F3 “Builder 生成器” as a functional/specification-style candidate from this ticket; keep it in the creational / GoF-gap survey.

## Longlist (new domains only)

No ranking, hard-gate pass/fail, or admission. Each is imaginable as a **lightweight runtime primitive** plus **attribute → generated registries / composition graphs / diagnostics / DI hooks**.

1. **Specification** — `ISpecification<T>` with `IsSatisfiedBy` plus And/Or/Not (and optional parameterized leaves); `[RegisterSpecification]` (or equivalent) → generated Keys / composition helpers / DI; covers Evans–Fowler selection, validation, and construction-to-order *uses* without becoming GoF Builder.
2. **Interpreter (expression / rule trees)** — leaf + composite expression nodes evaluated over a context; compile-time registration of operators/nodes and schema diagnostics (Fowler’s Composite Specification strategy explicitly uses GoF Interpreter—listed here as its own evaluable-expression domain, not only as Spec internals).
3. **Query Object (criteria-as-object)** — first-class selection criteria objects (often `Expression`-backed or repository-facing) that describe *what to fetch/filter*, distinct from pure in-memory `IsSatisfiedBy` evaluation; attribute catalog + missing/unknown-criteria diagnostics imaginable.
4. **Production Ruleset (condition → action)** — ordered or priority-keyed rules that pair a Specification-like condition with an effect/handler; generator wires rule order / Keys / duplicate-priority diagnostics (predicate composition alone stays in Specification; this domain adds the action side).

## Explicitly not listed as new domains

| Idea | Why omitted from this longlist |
|------|--------------------------------|
| GoF Builder / “Builder 生成器” | Creational F3 pool item; see distinction table above |
| Hard Coded / Parameterized / Composite Specification | Implementation *strategies* of Specification in Evans–Fowler—not separate domains |
| Subsumption / Partially Satisfied Specification | Companion operations on Specification (`isGeneralizationOf`, `remainderUnsatisfiedBy`)—fold into Specification if admitted |
| State transition guards / Strategy predicates | Enhancements or fragments of **shipped** domains, not new functional domains |
| Bare `Func<T,bool>` / LINQ `Where` wrappers | No compile-time glue surface beyond what BCL already provides |

## Non-decisions

- No hard-gate evaluation (ticket [#251]).
- No compile-time synergy ranking (ticket [#252]).
- No Top-3 / ROADMAP F3 write-back (tickets [#253]–[#254]).
- Map [#244] intentionally untouched.
