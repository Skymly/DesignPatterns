# DesignPatterns

.NET design-pattern primitives with compile-time registration glue (generators / analyzers). In-process libraries only — not a mediator product or actor runtime.

## Language

**Command Router**:
In-process 1:1 dispatch from a CLR command type to a single handler, with optional typed result.
_Avoid_: Mediator (product sense), Event Aggregator, message bus

**Command Pipeline Behavior**:
An ordered Chain-like onion stage around a command's terminal handler; receives the command and a `next` delegate and may short-circuit by omitting `next` (result path returns `TResult`).
_Avoid_: Decorator (for this capability), middleware (ambiguous), open-generic global behavior (not in current scope)

**Terminal handler**:
The single `ICommandHandler` registered for a command type; the innermost stage of the command pipeline onion.
_Avoid_: subscriber, strategy

**DI registration map**:
A compile-time type→lifetime view of explicit MSDI/Autofac registrations plus attributed `RegisterDi` expansions, including Singleton factory-delegate entries.
_Avoid_: IServiceCollection snapshot, container dump, RegisterDi argument-pair check (DP060/061)
