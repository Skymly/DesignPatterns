# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


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
