## Summary

<!-- What does this PR do and why? Write in English. -->

## Related Issue

Closes #

## Solution module

<!-- Must match a single module in AGENTS.md. Do not mix modules in one PR. -->

- [ ] Runtime (`DesignPatterns/`)
- [ ] Diagnostics (`DesignPatterns.Diagnostics/`)
- [ ] SourceGenerators (`DesignPatterns.SourceGenerators/`)
- [ ] Analyzers (`DesignPatterns.Analyzers/` + `DesignPatterns.CodeFixes/`)
- [ ] DependencyInjection (`DesignPatterns.Extensions.DependencyInjection/`)
- [ ] Package (`DesignPatterns.Package/`)
- [ ] Samples (`samples/`)
- [ ] Docs / Repository (README, `docs/`, `.github/`, `AGENTS.md`)

## Type of change

- [ ] Bug fix
- [ ] Feature
- [ ] Source generator / diagnostic / CodeFix change
- [ ] Refactor (no behavior change)
- [ ] Docs / repo metadata only

## Test plan

<!-- How did you verify? -->

- [ ] `dotnet build DesignPatterns.slnx -c Release`
- [ ] `dotnet test DesignPatterns.slnx -c Release`
- [ ] Samples build (if touched):
  ```
  
  ```

## Breaking changes

- [ ] None
- [ ] Yes — describe migration steps (APIs may still be pre-stable):

## Checklist

- [ ] This PR touches **only one** solution module (see [AGENTS.md](AGENTS.md))
- [ ] Commit messages are in **English** (no AI/agent tooling mentions in commits)
- [ ] No version bumps, tags, releases, or NuGet publish steps included unless explicitly requested
- [ ] Public API / diagnostic / generated code changes are documented if user-visible
