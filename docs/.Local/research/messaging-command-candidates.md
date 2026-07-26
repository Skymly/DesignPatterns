# Research: messaging / command-routing pattern candidates

- **Ticket**: [#247](https://github.com/Skymly/DesignPatterns/issues/247) (map [#244](https://github.com/Skymly/DesignPatterns/issues/244))
- **Question**: For the messaging / command routing family, which **new** pattern domain candidates fit DesignPatterns’ primitive + compile-time-glue model (including Command routing vs MediatR-shaped approaches)?
- **Scope**: longlist only; **new domains only**; note factual overlap with shipped Event Aggregator; **no admission decision**.
- **Branch**: `research/messaging-command-candidates`

## Primary sources

| Source | Role |
|--------|------|
| [`docs/design/EventAggregator.md`](../../design/EventAggregator.md) | Shipped pub/sub domain: API, generator, diagnostics, MediatR boundary table, known non-goals |
| [`DesignPatterns/Behavioral/IEventAggregator.cs`](../../../DesignPatterns/Behavioral/IEventAggregator.cs), [`IEventHandler.cs`](../../../DesignPatterns/Behavioral/IEventHandler.cs), [`EventAggregator.cs`](../../../DesignPatterns/Behavioral/EventAggregator.cs) | Runtime facts: typed Subscribe/Unsubscribe + sequential `PublishAsync` (1:N) |
| [`docs/ROADMAP.md`](../../ROADMAP.md) § F3 | Candidate pool explicitly lists **Command 路由（探索与 MediatR 的差异点）**; overlap with MediatR is not a rejection reason |
| [MediatR README](https://github.com/LuckyPennySoftware/MediatR/blob/main/README.md) | Official surface: request/response, commands, queries, notifications/events, stream requests, pipeline behaviors / pre-post processors |
| [MediatR.Contracts](https://github.com/LuckyPennySoftware/MediatR/tree/main/src/MediatR.Contracts) | `IRequest` / `IRequest<TResponse>`, `INotification`, `IStreamRequest` |
| [MediatR `IPipelineBehavior`](https://github.com/LuckyPennySoftware/MediatR/blob/main/src/MediatR/IPipelineBehavior.cs) | Request-only pipeline wrapper around a single handler continuation |
| [MediatR #163](https://github.com/LuckyPennySoftware/MediatR/issues/163) (maintainer) | Requests = 1→1 (commands/queries); Notifications = 1→N (events) |
| [MediatR #353](https://github.com/LuckyPennySoftware/MediatR/issues/353) (maintainer) | Pipeline behaviors are explicitly for requests, not notifications |

## Shipped baseline (Event Aggregator) — overlap facts

DesignPatterns already ships an in-process **typed pub/sub** domain:

- Runtime: `IEventHandler<TEvent>` + `IEventAggregator` (`Subscribe` / `Unsubscribe` / `PublishAsync` / traced variants).
- Compile-time glue: `[RegisterEventHandler]` → `{Event}EventHandlerRegistry` (`SubscribeAll` / optional `RegisterDi`) + DP044–DP046.
- Semantics: **one event type → N handlers**, sequential await, fire-and-forget style (no response value).
- Design doc boundary vs MediatR: EA is type-routed pub/sub; MediatR adds request/notification models and pipelines ([EventAggregator.md § 与生态的边界](../../design/EventAggregator.md)).
- Explicit non-goals today: cross-process, persistence, retry, **request/response correlation IDs** ([EventAggregator.md § 已知局限](../../design/EventAggregator.md)).

**Factual MediatR ↔ EA overlap**: MediatR `INotification` / `IPublisher` (1→N, no response) occupies the same *messaging role* as shipped Event Aggregator. Candidates below that restate notification pub/sub are **not** new domains for this map family.

**Factual MediatR shapes not covered by EA**: `IRequest` / `IRequestHandler` (1→1, optional `TResponse`), `IPipelineBehavior` / pre-post processors (request pipeline only), `IStreamRequest` (progressive responses). ROADMAP F3 already names **Command routing** as the MediatR-difference exploration point.

## Longlist (new domains only)

No ranking, hard-gate pass/fail, or admission.

1. **Command Router (request/response)** — 1:1 typed `Send`/`TrySend` over `ICommand`/`ICommandHandler<TCommand,TResult>` with `[RegisterCommandHandler]` → generated registry + missing/duplicate-handler diagnostics (MediatR `IRequest` shape; **not** EA’s 1:N `Publish`).
2. **Request Pipeline Behaviors** — ordered cross-cutting wrappers around a single command handler continuation, composed by attribute/order metadata at compile time (MediatR `IPipelineBehavior` / pre-post processors; EA has sequential fan-out, not a request/response pipeline).
3. **Stream Request Router** — 1:1 progressive `IAsyncEnumerable<T>` (or equivalent) responses with compile-time handler binding (MediatR `IStreamRequest`; no EA analogue).
4. **Correlated Request/Reply Messenger** — in-process request/reply with correlation id + typed reply routing via generated glue (addresses EA’s documented non-goal “不做请求/响应关联 ID”; distinct from sync Command Router if framed as async correlate/reply rather than direct `Send`).

## Explicitly not listed as new domains (near-duplicates / enhancements)

| Idea | Why omitted from this longlist |
|------|--------------------------------|
| Observer / light pub-sub extension | ROADMAP F3 wording; map [#244] treats existing-pattern enhancements as out of shortlist; role overlaps shipped Event Aggregator |
| Notification bus / typed multicast publisher | Same 1:N role as EA / MediatR `INotification` |
| “Mediator” umbrella replicating MediatR’s combined `ISender`+`IPublisher` | Would fold EA + Command Router; not a single new domain for longlisting |

## Non-decisions

- No hard-gate evaluation (ticket [#251]).
- No compile-time synergy ranking (ticket [#252]).
- No Top-3 / ROADMAP F3 write-back (tickets [#253]–[#254]).
- Map [#244] intentionally untouched.
