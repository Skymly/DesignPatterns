# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues on **`Skymly/DesignPatterns`**. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --repo Skymly/DesignPatterns --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <n> --repo Skymly/DesignPatterns --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --repo Skymly/DesignPatterns --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <n> --repo Skymly/DesignPatterns --body "..."`
- **Apply / remove labels**: `gh issue edit <n> --repo Skymly/DesignPatterns --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <n> --repo Skymly/DesignPatterns --comment "..."`

When the shell cwd is this clone, `gh` infers the repo from `git remote` — prefer explicit `--repo` only when routing across siblings (below).

## Pull requests as a triage surface

**PRs as a request surface: no.**

## When a skill says "publish to the issue tracker"

Create a GitHub issue **in the owning repo** (see routing table). Do not invent a local markdown issue.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <n> --comments` against the owning repo.

## Sibling issue routing (authoritative)

This monorepo-of-siblings layout:

| Local path | GitHub repo |
|------------|-------------|
| `C:\Code\Skymly\DesignPatterns\DesignPatterns` | `Skymly/DesignPatterns` |
| `C:\Code\Skymly\DesignPatterns\DesignPatterns.Docs` | `Skymly/DesignPatterns.Docs` |
| `C:\Code\Skymly\DesignPatterns\DesignPatterns.Samples` | `Skymly/DesignPatterns.Samples` |

**Execution issues follow the code/site they change.** Do not open a duplicate full ticket in this repo for work that only lands in Docs or Samples.

| Work belongs in… | File the execution issue in… | Do **not** file execution issue here for… |
|------------------|------------------------------|-------------------------------------------|
| Runtime / Diagnostics / Generators / Analyzers / DI / Autofac / Package / this repo `docs/` (ADR, Design Doc, ROADMAP, `AGENTS.md`, maintainer notes) | **`Skymly/DesignPatterns`** | — |
| User-facing VitePress site (`docs/`, `docs/zh/`, site config) | **`Skymly/DesignPatterns.Docs`** | Pure user-guide / diagnostics-page / samples.md edits |
| Runnable console samples | **`Skymly/DesignPatterns.Samples`** | Sample-only code or sample CI |

### Cross-repo association (preferred pattern)

When a feature spans library + user docs + samples:

1. Keep the **parent / wayfinder:map / epic** (coordination checklist) in **`Skymly/DesignPatterns`**.
2. Open **child execution issues** in Docs and/or Samples with `gh issue create --repo Skymly/DesignPatterns.Docs|Samples`.
3. Link both ways — do **not** copy the full acceptance criteria into two repos:
   - Parent checklist item: `- [ ] Skymly/DesignPatterns.Docs#N — …`
   - Child body top: `Relates to: https://github.com/Skymly/DesignPatterns/issues/M` (and `Blocked by: …` when gated on library merge).
4. Prefer GitHub native dependencies when available; otherwise `Blocked by:` / `Relates to:` lines.

### Anti-patterns

- Opening `Docs: …` or `Samples: …` execution tickets in this repo that only edit the sibling clone.
- Dual-filing the same acceptance criteria in two repos (status drift).
- Implementing Docs/Samples work from a DesignPatterns issue without switching cwd / `--repo` to the sibling.

### Distinguishing “Docs” titles

- **This repo** issue titled `Docs: …` means maintainer docs under **this** `docs/` (e.g. ADR, Design Doc) — correct here.
- User site work must be titled/filed on **`DesignPatterns.Docs`**, not as a DesignPatterns execution issue.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. `gh issue create --repo Skymly/DesignPatterns --label wayfinder:map`.
- **Child ticket**: prefer a GitHub sub-issue of the map. Where sub-issues aren't enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of the child body. Labels: `wayfinder:research` / `wayfinder:prototype` / `wayfinder:grilling` / `wayfinder:task`. Once claimed, assign to the driving dev.
- **Cross-repo children**: Docs/Samples children are **not** GitHub sub-issues of a DesignPatterns map (different repos). Represent them only as checklist URLs + `Relates to` / `Blocked by` on both sides. Still apply the matching `wayfinder:*` label **in the child repo**.
- **Blocking**: GitHub native issue dependencies when same-repo; otherwise `Blocked by: #n` or full URL for cross-repo.
- **Frontier query**: list the map's open children; drop any with an open blocker or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me` — the session's first write.
- **Resolve**: `gh issue comment` with outcome, `gh issue close`, then append a context pointer to the map's Decisions-so-far.
