# Agent instructions

## OKF knowledge set

This repository uses `.okf/` as compact operational memory for AI agents.

OKF v0.1: non-reserved `.md` files need YAML frontmatter with non-empty `type`, `title`, and `description`; `index.md` is a directory listing; only bundle-root `index.md` may have frontmatter (`okf_version`).

### Before work

Read relevant OKF files before editing. Start with `.okf/index.md`, then overview, architecture, code-map, conventions as needed; add testing/workflows/dependencies/operations/gotchas (and monorepo-packages/generated-code/security when relevant) for the task. OKF guides—it does not replace checking source, tests, or manifests for exact behavior.

### During work

Preserve conventions, boundaries, and workflows from OKF. On conflict with source/tests/generated artifacts/manifests: prefer verified current behavior, update OKF, mention the correction.

### Before finishing

Update `.okf/` when the change affects architecture/boundaries; public APIs/routes/schemas/events/contracts; persistence/migrations; deps/runtime; build/run/test/lint/format/generate/deploy commands; testing strategy; security/auth; config/env/ports/ops; conventions/layout; or gotchas. If no update needed, state why (pure comment/typo/formatting: `OKF unaffected (non-behavioral edit)`). Task is incomplete until OKF is synced or explicitly unaffected.

For **user-visible** library changes (public APIs, config, behavior, breaking changes, new features), also update the public docs in sibling `../FE-Docs/src/content/docs/` (see `.okf/workflows.md`). Do not copy full doc pages into OKF.

### General

Subject to project conventions in OKF/`conventions.md` and this file:

- Focused, minimal changes; prefer existing patterns.
- Do not edit generated files unless the project requires it.
- Run relevant validation when practical.
- Prefer project references and central package versions (`Directory.Packages.props`); do not invent reverse package dependencies.
- Keep Agents addons independently versioned when touching `Src/Agents/`.
