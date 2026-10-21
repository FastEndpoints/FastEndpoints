# Agent instructions

## OKF knowledge set

This repository uses `.okf/` as compact operational memory for AI agents.

OKF v0.1: non-reserved `.md` files need YAML frontmatter with non-empty `type`, `title`, and `description`; `index.md` is a directory listing; only bundle-root `index.md` may have frontmatter (`okf_version`).

Normative OKF use/update gates live in this file. `.okf/index.md` and `.okf/maintenance.md` are reminders and conformance detail; the okf-setup skill is setup/maintain procedure.

### Before work

Match OKF depth to blast radius:

- Local/small change: `.okf/index.md` + conventions/gotchas (and the matching task file if the surface is already documented).
- Cross-cutting, public API, persistence, auth, contracts, or new surface: core set first (overview, architecture, code-map, conventions), then task-specific files (testing/workflows/dependencies/gotchas and any expanded files present: monorepo-packages/generated-code).

OKF guides; it does not replace checking source, tests, or manifests for exact behavior.

### During work

Preserve conventions, boundaries, and workflows from OKF. On conflict with source/tests/generated artifacts/manifests: prefer verified current behavior, update OKF, mention the correction.

### OKF content discipline

- OKF is operational memory, not a change log or second documentation site.
- Add facts only when they guide future decisions: boundaries, contracts, commands, non-obvious failure modes, or owner-approved policies. Omit routine implementation details and exhaustive API/test inventories.
- Keep each detailed fact in one topic owner. Elsewhere, use a short summary or link only when needed for navigation.
- Update or replace existing guidance rather than appending another account of the same behavior. Remove superseded material in the same edit.
- Test guidance should identify relevant suites, commands, setup, and isolation requirements, not enumerate assertions.
- Before finishing an OKF edit, check for duplication, stale detail, and unnecessary reading cost. Preserve critical safety, compatibility, persistence, and owner-decision constraints.

### Before finishing

Evaluate OKF when a change hits the update triggers in `.okf/maintenance.md`. Update it only when existing guidance becomes incorrect or incomplete, or the change introduces durable operational knowledge that future agents would otherwise need to rediscover. A trigger requires evaluation, not an automatic addition. If existing guidance remains sufficient, report `OKF unaffected (existing guidance remains accurate and sufficient)`.

If no update needed, state why (pure comment/typo/formatting: `OKF unaffected (non-behavioral edit)`). Task is incomplete until OKF is synced or explicitly unaffected.

For **user-visible** library changes (public APIs, config, behavior, breaking changes, new features), also:

- update public docs in sibling `../FE-Docs/src/content/docs/` (see `.okf/workflows.md`). Do not copy full doc pages into OKF.
- add or update an entry in `Src/Library/changelog.md` (format in `.okf/workflows.md`). Skip tests-only, OKF, comments, CI, formatting, and internal refactors with no consumer effect.

### General

Subject to project conventions in OKF/`conventions.md` (and architecture when present) and this file:

- Focused, minimal changes; prefer existing patterns.
- Do not use em or en dashes as prose punctuation. Use commas, parentheses, or separate sentences instead. Hyphens remain valid in compound words, bullets, code, commands, CLI flags, identifiers, paths, versions, ranges, quotations, and exact terms.
- Do not hand-edit generated artifacts listed in code-map/gotchas; regenerate via project commands instead. If a path is not listed but is clearly generated output, leave it alone and regenerate.
- If behavior changes, run the smallest relevant command from workflows/testing. If not run, state the blocker.
- Prefer project references and central package versions (`Directory.Packages.props`); do not invent reverse package dependencies.
- Keep Agents addons independently versioned when touching `Src/Agents/`.
