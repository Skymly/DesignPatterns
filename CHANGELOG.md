# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview2] - 2026-06-14

Aligns the publish tag with current `main` (includes F2 handler trace, composite forest, decorator conditions, and async strategy paths). Same package surface as `0.1.0-preview1` plus those runtime and generator updates.

### Added

- Same meta-package contents as `0.1.0-preview1`; published from commit synchronized with `main`.

## [0.1.0-preview1] - 2026-06-14

First public NuGet preview of the `DesignPatterns` meta-package. APIs, generated code shapes, and `DP###` diagnostics remain **unstable** — pin a version or commit if you depend on this library.

### Added

- **Runtime primitives** for Singleton, Factory Registry, Strategy, Chain of Responsibility, Composite, Decorator, and Event Aggregator (`netstandard2.0` + `net8.0`).
- **Source generators**: `[GenerateSingleton]`, `[RegisterFactory]`, `[RegisterStrategy]`, `[HandlerOrder]`, `[CompositePart]`, `[Decorator]`.
- **Analyzers and CodeFixes**: DP006 (unregistered strategy), DP023 (unregistered factory), DP024 (unregistered handler), DP025 (unknown registry key literal).
- **Generator diagnostics** DP001–DP022 for compile-time validation of attributes and generated registry shapes.
- **`DesignPatterns` NuGet meta-package** bundling runtime (`lib/netstandard2.0`, `lib/net8.0`), source generator, analyzers, and code fixes.
- **`DesignPatterns.Extensions.DependencyInjection`** (solution project only; not included in the meta-package) with `RegisterDi` / `Create(IServiceProvider)` integration.
- Nuke `Ci` / `CiPack` build targets, pack verification, and consumer smoke test.
- GitHub Actions CI (build, test, sibling Samples, pack).

### Notes

- Early preview: breaking changes may land without a major version bump until an API stability announcement.
- DI extension package publishing strategy is still TBD; reference the project from source or a future package release.

[0.1.0-preview2]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview2
[0.1.0-preview1]: https://github.com/Skymly/DesignPatterns/releases/tag/v0.1.0-preview1
