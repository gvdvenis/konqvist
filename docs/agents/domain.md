# Domain Docs

How engineering skills should consume this repo's domain documentation.

## Before exploring, read these

- `CONTEXT.md` at the repo root.
- `CONTEXT-MAP.md` at the root if it exists (then read each relevant context `CONTEXT.md`).
- `docs/adr/` ADRs related to the area being changed.
- In multi-context repos, also check `src/<context>/docs/adr/` where present.

If any file is missing, proceed silently.

## File structure

Single-context repo (this repository):

```
/
├── CONTEXT.md
├── docs/adr/
└── src/
```

Multi-context repos (if `CONTEXT-MAP.md` exists):

```
/
├── CONTEXT-MAP.md
├── docs/adr/
└── src/<context>/
    ├── CONTEXT.md
    └── docs/adr/
```

## Vocabulary and ADR conflicts

- Prefer the exact terms defined in `CONTEXT.md` when naming concepts.
- If a proposal conflicts with an ADR, surface the ADR conflict explicitly instead of silently overriding it.
