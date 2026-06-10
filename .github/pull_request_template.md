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
- [ ] Docs / Repository (README, `docs/`, `.github/`, `AGENTS.md`, `build/`)

## Type of change

- [ ] Bug fix
- [ ] Feature
- [ ] Source generator / diagnostic / CodeFix change
- [ ] Refactor (no behavior change)
- [ ] Docs / repo metadata only

## Test plan

<!-- How did you verify? -->

- [ ] `./build.ps1 --target Ci --configuration Release` (or `CiPack` if packaging changed)
- [ ] Sibling samples (if API/generator behavior changed): clone [DesignPatterns.Samples](https://github.com/Skymly/DesignPatterns.Samples) beside this repo and run `./build.ps1 --target Ci`

## Breaking changes

- [ ] None
- [ ] Yes — describe migration steps (APIs may still be pre-stable):

## Checklist

- [ ] This PR touches **only one** solution module (see [AGENTS.md](AGENTS.md))
- [ ] Commit messages are in **English** (no AI/agent tooling mentions in commits)
- [ ] PR description is **English** only — no AI/agent/Cursor tool attribution, no auto-generated summary blocks (e.g. `CURSOR_SUMMARY`, "Made with …")
- [ ] No version bumps, tags, releases, or NuGet publish steps included unless explicitly requested
- [ ] Public API / diagnostic / generated code changes are documented if user-visible
