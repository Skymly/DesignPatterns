# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`AGENTS.md`** at the repo root (project status, module boundaries, diagnostics registry, coding standards).
- **`docs/DOCUMENTATION.md`** — documentation carriers and sync checklist.
- **`docs/adr/`** — ADRs that touch the area you're about to work in.
- **`docs/design/`** — Design Doc for the pattern domain under change.
- Sibling routing for issues: **`docs/agents/issue-tracker.md`**.

If a listed optional file doesn't exist, **proceed silently**. Don't flag absence; don't suggest creating domain files upfront unless the task requires an ADR / Design Doc update per `AGENTS.md`.

## File structure

```
/
├── AGENTS.md
├── docs/
│   ├── agents/          ← skills config (this folder)
│   ├── adr/
│   ├── design/
│   ├── DOCUMENTATION.md
│   ├── DEVELOPMENT.md
│   └── ROADMAP.md
└── DesignPatterns/      ← runtime, etc.
```

User-facing guides and runnable samples are **not** in this tree — see sibling clones:

- `../DesignPatterns.Docs/` → `Skymly/DesignPatterns.Docs`
- `../DesignPatterns.Samples/` → `Skymly/DesignPatterns.Samples`

## Use the glossary's vocabulary

Prefer terms already used in `AGENTS.md`, Design Docs, and ADRs (`IFactoryRegistry`, `RegisterDi`, `DP###`, module names). Don't invent parallel names for the same concept.

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding.
