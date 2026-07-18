# Agent instructions

## OKF knowledge set

This repository uses `.okf/` as compact operational memory for AI agents.

OKF v0.1: non-reserved `.md` files need YAML frontmatter with non-empty `type`, `title`, and `description`; `index.md` is a directory listing; only bundle-root `index.md` may have frontmatter (`okf_version`).

Normative OKF use/update gates live in this file. `.okf/index.md` and `.okf/maintenance.md` are reminders and conformance detail; the okf-setup skill is setup/maintain procedure.

### Before work

Match OKF depth to blast radius:

- Local/small change: `.okf/index.md` + conventions/gotchas (and the matching task file if the surface is already documented).
- Cross-cutting, public API, persistence, auth, contracts, or new surface: core set first — overview, architecture, code-map, conventions — then task-specific files (testing/workflows/dependencies/operations/gotchas and any expanded files present: monorepo-packages/generated-code/security).

OKF guides—it does not replace checking source, tests, or manifests for exact behavior.

### During work

Preserve conventions, boundaries, and workflows from OKF. On conflict with source/tests/generated artifacts/manifests: prefer verified current behavior, update OKF, mention the correction.

### Before finishing

Sync `.okf/` when the change hits the update triggers in `.okf/maintenance.md`. Default trigger inventory: architecture/boundaries; public APIs/routes/schemas/events/contracts; persistence/migrations; deps/runtime; build/run/test/lint/format/generate/deploy; testing strategy; security/auth; config/env/ports/ops; conventions/layout; gotchas.

If no update needed, state why (pure comment/typo/formatting: `OKF unaffected (non-behavioral edit)`). Task is incomplete until OKF is synced or explicitly unaffected.

For **user-visible** library changes (public APIs, config, behavior, breaking changes, new features), also update the public docs in sibling `../FE-Docs/src/content/docs/` (see `.okf/workflows.md`). Do not copy full doc pages into OKF.

### General

Subject to project conventions in OKF/`conventions.md` (and architecture when present) and this file:

- Focused, minimal changes; prefer existing patterns.
- Do not hand-edit generated artifacts listed in code-map/gotchas; regenerate via project commands instead. If a path is not listed but is clearly generated output, leave it alone and regenerate.
- If behavior changes, run the smallest relevant command from workflows/testing. If not run, state the blocker.
- Prefer project references and central package versions (`Directory.Packages.props`); do not invent reverse package dependencies.
- Keep Agents addons independently versioned when touching `Src/Agents/`.
