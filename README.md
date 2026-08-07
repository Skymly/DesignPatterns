# DesignPatterns

[![CI](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml/badge.svg)](https://github.com/Skymly/DesignPatterns/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Skymly.DesignPatterns?label=NuGet)](https://www.nuget.org/packages/Skymly.DesignPatterns)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://skymly.github.io/DesignPatterns.Docs/)

**Lightweight runtime primitives + Roslyn source generators** for .NET design patterns.

Mark implementations with attributes; the generators emit keys, registries, and pipelines, and analyzers catch misuse at compile time. Overlap with libraries such as MediatR, Polly, or Stateless is fine — this project explores compile-time glue around small primitives, not a one-framework-to-rule-them-all product.

> **Early preview.** Public APIs, generated code shapes, and `DP###` diagnostics are **not stable** yet. Pin a package version (or a Git commit) until a stability announcement.

## Install

```xml
<PackageReference Include="Skymly.DesignPatterns" Version="0.2.4-preview1" />
```

```powershell
dotnet add package Skymly.DesignPatterns --version 0.2.4-preview1
```

C# namespaces remain `DesignPatterns.*`. Full guides: [Getting started](https://skymly.github.io/DesignPatterns.Docs/getting-started.html) · [中文文档](https://skymly.github.io/DesignPatterns.Docs/zh/)

## Quick example

```csharp
using DesignPatterns.Behavioral;

public interface IPaymentStrategy
{
    string Pay(decimal amount);
}

[RegisterStrategy<IPaymentStrategy>("alipay")]
public sealed class AlipayPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Alipay: {amount:C}";
}

// Generated: PaymentStrategyKeys + PaymentStrategyRegistry
var pay = PaymentStrategyRegistry.Instance.Get(PaymentStrategyKeys.Alipay);
Console.WriteLine(pay.Pay(100m));
```

## What's included

| Pattern | What you get |
|---------|----------------|
| [Singleton](https://skymly.github.io/DesignPatterns.Docs/singleton.html) | `[GenerateSingleton]` → lazy `Instance` |
| [Factory Registry](https://skymly.github.io/DesignPatterns.Docs/factory-registry.html) | `[RegisterFactory]` → keys + registry (+ async / pooling) |
| [Strategy](https://skymly.github.io/DesignPatterns.Docs/strategy.html) | `[RegisterStrategy]` → keys + registry (+ guards / tracing) |
| [Chain of Responsibility](https://skymly.github.io/DesignPatterns.Docs/chain-of-responsibility.html) | `[HandlerOrder]` → ordered pipeline (+ guards / tracing) |
| [Composite](https://skymly.github.io/DesignPatterns.Docs/composite.html) | `[CompositePart]` → catalog, forest build, traversal |
| [Decorator](https://skymly.github.io/DesignPatterns.Docs/decorator.html) | `[Decorator]` → ordered stack build |
| [Event Aggregator](https://skymly.github.io/DesignPatterns.Docs/event-aggregator.html) | `[RegisterEventHandler]` → subscribe-all registries |
| [State](https://skymly.github.io/DesignPatterns.Docs/state-transition-table.html) | `[StateMachine]` / `[Transition]` → tables, guards, hierarchy |
| [Command Router](docs/design/CommandRouter.md) | `[RegisterCommandHandler]` → 1:1 dispatch + optional pipeline |
| [Step Builder](docs/design/StepBuilder.md) | `[GenerateBuilder]` → compile-time type-state builder |
| [Work Graph](docs/design/WorkGraph.md) | `[WorkGraph]` / `[WorkStep]` → fork–join DAG waves |

Diagnostics (`DP###`), CodeFixes, and deeper API notes live in the [documentation site](https://skymly.github.io/DesignPatterns.Docs/) and maintainer [design docs](docs/design/README.md).

## Docs & samples

| Resource | Purpose |
|----------|---------|
| [DesignPatterns.Docs](https://skymly.github.io/DesignPatterns.Docs/) | User guides (EN / 中文) |
| [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) | Runnable console samples |
| [ROADMAP](docs/ROADMAP.md) | Backlog and exploration candidates |
| [CHANGELOG](CHANGELOG.md) | Release notes |

## Contributing

Issues and PRs are welcome — please use **English** for Issue / PR / commit text. See [CONTRIBUTING.md](CONTRIBUTING.md) for build, test, and module boundaries.

```powershell
./build.ps1 --target Ci --configuration Release
```

## License

[MIT](LICENSE)
