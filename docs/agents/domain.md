# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`docs/CONTEXT.md`** — the single-context glossary (GitPulse keeps it under `docs/` per [DOCUMENTATION.md](../DOCUMENTATION.md), not at the repo root).
- **`docs/adr/`** — read ADRs that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

When writing new glossary entries, append to `docs/CONTEXT.md` using that file's existing term format. When writing a new ADR, use `docs/adr/_template.md` and the next `ADR-NNN` number — do not invent a parallel ADR series at the repo root.

## File structure

Single-context repo:

```
/
├── docs/
│   ├── CONTEXT.md
│   └── adr/
│       ├── ADR-001-layered-solution-architecture.md
│       └── …
└── src/
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `docs/CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (event-sourced orders) — but worth reopening because…_
