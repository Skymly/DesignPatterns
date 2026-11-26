# DesignPatterns

A .NET design patterns toolkit combining lightweight, composable runtime primitives with Roslyn incremental source generators, analyzers, and code fixes (`Skymly.DesignPatterns`, early pre-stable preview). Companion repositories: [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) and [DesignPatterns.Docs](https://github.com/Skymly/DesignPatterns.Docs).

## Build & Verify

```powershell
# Pre-PR gate (Clean -> Restore -> Compile -> UnitTest):
./build.ps1 --target Ci --configuration Release

# Full pack & consumer smoke gate (adds Pack, PackVerify, PackConsumerVerify, NuGetConsumerSmoke):
./build.ps1 --target CiPack --configuration Release
```

## Architecture & Invariants

- **Design principles**: Prefer composable primitives over heavy base-class hierarchies; `DesignPatterns` (Core) must **never** reference `Microsoft.Extensions.DependencyInjection` (DI and Autofac integrations live in `DesignPatterns.Extensions.DependencyInjection` and `DesignPatterns.Extensions.Autofac`); treat async (`CancellationToken` / `ValueTask`) as first-class; prefer explicit failure (`TryResolve` and clear exceptions) over silent `null`.
- **Compatibility baseline**: Runtime multi-targets `netstandard2.0` and `net8.0`. Roslyn components (`DesignPatterns.SourceGenerators`, `DesignPatterns.Analyzers`, `DesignPatterns.CodeFixes`) stay pinned to **Roslyn 4.8.0** (`IIncrementalGenerator` + `ForAttributeWithMetadataName`) for broad IDE/SDK compatibility.
- **Meta-package invariant**: `Skymly.DesignPatterns` (`DesignPatterns.Package/`) must pack **both** `netstandard2.0` and `net8.0` runtime `lib/` assemblies (so `net8.0` consumers get `FrozenDictionary` optimizations) plus `analyzers/dotnet/cs`. Verify any packaging change with `CiPack`.
- **Single-module PR boundary**: Keep each Issue + PR within one module from [`DesignPatterns.slnx`](DesignPatterns.slnx): `Runtime` (`DesignPatterns/`), `Diagnostics` (`DesignPatterns.Diagnostics/`), `SourceGenerators`, `Analyzers` (+ `CodeFixes`), `DependencyInjection`, `Autofac`, `Package`, `Docs` (maintainer `docs/` only), or `Repository` (`.github/`, `build/`, root files). File `.Docs` and `.Samples` execution issues in those sibling repositories.

## Diagnostics & Gotchas

- **Diagnostic SSOT**: [`DesignPatterns.Diagnostics/DiagnosticIds.cs`](DesignPatterns.Diagnostics/DiagnosticIds.cs), [`DesignPatternsDiagnosticDescriptors.cs`](DesignPatterns.Diagnostics/DesignPatternsDiagnosticDescriptors.cs), and [`DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md`](DesignPatterns.SourceGenerators/AnalyzerReleases.Unshipped.md) are the canonical registry (next available ID is documented in `DiagnosticIds.cs` / `AnalyzerReleases.Unshipped.md`). Never reuse or repurpose published `DP###` IDs (`DP067–DP071` are reserved by [ADR-008](docs/adr/ADR-008-singleton-lifecycle-diagnostics.md)); keep [`DiagnosticHelpLinks.cs`](DesignPatterns.Diagnostics/DiagnosticHelpLinks.cs), the corresponding Design Doc, and `DesignPatterns.Docs` in sync.
- **No bundled CompletionProvider**: Do not ship custom Roslyn `CompletionProvider` types in the NuGet meta-package (IDE hosts do not load project-referenced completion providers; literal keys are checked by analyzers instead).
- **Coding & tests**: `TreatWarningsAsErrors=true`, repo-wide `Nullable=enable`, file-scoped namespaces, XML docs on public APIs (`GenerateDocumentationFile`), and a single xUnit version in [`Directory.Packages.props`](Directory.Packages.props). Review Verify `*.received.*` snapshots carefully before promoting to `*.verified.txt`.

## Docs & Agent Config

- Domain glossary: [`CONTEXT.md`](CONTEXT.md)
- Maintainer docs: [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md), [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md), [`docs/PUBLISHING.md`](docs/PUBLISHING.md), [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/adr/`](docs/adr/), [`docs/design/`](docs/design/), [`CHANGELOG.md`](CHANGELOG.md)
- Issue routing & triage labels: [`docs/agents/`](docs/agents/)

## Repo-Specific Conventions

- Commit messages, issues, and PRs: **English**. Never mention AI coding tools or LLM vendors in Git-visible content.
- Version SSOT is [`Directory.Build.props`](Directory.Build.props) (`VersionPrefix` + `VersionSuffix`).
- `v*` preview tags publish to nuget.org and GitHub Packages via `release.yml` without creating a GitHub Release; do not bump versions, tag, or publish unless explicitly requested.
