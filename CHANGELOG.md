# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).



## [Unreleased]

### Changed

- **Result&lt;T&gt; pipeline model**: introduced `Result<T>` and `DiagnosticInfo` in `DesignPatterns.SourceGenerators` so per-target diagnostics flow through the incremental pipeline alongside extracted models instead of being silently dropped when `Transform` returns `null`. Refactored `GenerateSingletonGenerator`, `DecoratorGenerator`, `CompositePartGenerator`, and `StateTransitionGenerator` to use the new pattern. `HandlerOrderGenerator` and `RegistrationGeneratorHelper` (which return `List<T>`, not `T?`) are unchanged.
- Made `EquatableArray<T>` null-safe for `default` instances to support `Result<T>.Empty` / `Result<T>.Success` without allocation.
- **Modular `StateTransitionGenerator`**: split the 455-line monolith into `Generators/StateTransition/` subfolder with `StateTransitionModels.cs` (model records), `StateTransitionTransform.cs` (extraction), `StateTransitionValidator.cs` (DP026–DP031 diagnostics), and `StateTransitionEmitter.cs` (code emission). `StateTransitionGenerator.cs` is now a 55-line pipeline entry point (`Initialize` + `Execute`). Mirrors the Vogen `GenerateCodeFor*.cs` pattern and prepares for State v2.

### Added

- **State v2 guard delegates (runtime)**: `TransitionTableBuilder.Add(from, trigger, to, guard: Func<TState, TTrigger, bool>?)` — optional guard delegate evaluated by `TryTransition` before firing. When the guard returns `false`, the transition is treated as if it does not exist. `ITransitionTable<TState, TTrigger>` interface unchanged; guards are transparent to callers. `TransitionAttribute.Guard` property added for future source generator support.

## [0.1.0-preview7] - 2026-06-20

### Fixed

- **Incremental cache correctness**: source generator models no longer store `ISymbol` / `SyntaxNode`; switched to record types with fully-qualified names and `Location`, restoring proper incremental cache invalidation across all 7 generators.

### Changed

- Extracted `KeyedRegistrationGeneratorBase` to eliminate pipeline duplication between `RegisterFactoryGenerator` and `RegisterStrategyGenerator`.
- Added `WithTrackingName` to generator pipelines and added cache-hit regression tests.
- Migrated analyzer tests to Verify snapshots for consistency with generator tests.

[0.1.0-preview7]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview7

## [0.1.0-preview6] - 2026-06-16

### Added

- **DP033** analyzer: reports duplicate strategy keys for the same contract across multiple referenced plugin provider assemblies.

[0.1.0-preview6]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview6

## [0.1.0-preview5] - 2026-06-16

### Added

- **Autofac extension**: `DesignPatterns.Extensions.Autofac` with `RegisterAutofac` source generator for Autofac module registration (optional assembly; not in the meta-package).
- **AppSettings extension**: `DesignPatterns.Extensions.AppSettings` with `RegistryConfiguration` helpers bridging `ConfigurationManager.AppSettings` to strategy registries.
- **Configuration extension**: `DesignPatterns.Extensions.Configuration` with `RegistryConfiguration` helpers bridging `IConfiguration` to strategy registries.
- **Plugin assemblies** documentation (`docs/PluginAssemblies.md`, `docs/AppSettings.md`) and cross-project registry generator tests.
- **net48 NuGet smoke**: `eng/nuget-smoke/MetaPackage.Consumer.Net48` compiled in `CiPack` (build-only on Linux CI).

[0.1.0-preview5]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview5

## [0.1.0-preview4] - 2026-06-15

### Added

- **State transition table** (M1): `ITransitionTable<TState,TTrigger>`, `TransitionTableBuilder`, `InvalidTransitionException`, and `Transition()` extension.
- **State transition table** (M2): `[StateMachine]` / `[Transition]` source generator emitting `{State}TransitionTable` and holder `TryTransition`; generator diagnostics **DP026–DP031**.

[0.1.0-preview4]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview4

## [0.1.0-preview3] - 2026-06-14

First public preview on nuget.org under **`Skymly.DesignPatterns`**. Supersedes deprecated GitHub-only packages `DesignPatterns` `0.1.0-preview1` / `0.1.0-preview2`.

### Changed

- **PackageId** `Skymly.DesignPatterns` (was `DesignPatterns`; nuget.org ID taken by a third party).

### Added

Same meta-package surface as prior previews: runtime (`netstandard2.0`, `net8.0`), source generators, analyzers DP006/023/024/025, generator diagnostics DP001–DP022, and CodeFixes.

[0.1.0-preview3]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview3
