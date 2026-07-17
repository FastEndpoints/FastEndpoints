---
type: Reference
title: Maintenance
description: How to keep the OKF set conformant and when to update it.
tags: [maintain]
---

# Maintenance

## Conformance
- OKF v0.1: non-reserved `.md` files need YAML frontmatter with non-empty `type` (closed list), `title`, `description`.
- Bundle-root `index.md` is the router; only it may set `okf_version: "0.1"`.
- Do not invent `type` values; use `Reference` if unsure.
- No secrets in OKF. Prefer `## Sources` for multi-source claims.
- Soft target ~50–150 lines per file; split by topic when scanning suffers.
- Day-to-day OKF finish gate is normative in `AGENTS.md`; this file is the trigger inventory + conformance reminder.

## Update triggers
Sync OKF when changes hit:
- architecture / package boundaries / dependency directions
- public APIs, endpoint pipeline contracts, messaging/job contracts
- persistence SPIs (e.g. job storage interfaces)
- deps / TFMs / central package management
- build, test, pack, publish, codegen commands
- testing strategy or harness layout
- security/auth surfaces
- config/env/ops assumptions
- conventions, layout, gotchas
- docs maintenance workflow / FE-Docs paths (not the full docs body)

**Public docs (FE-Docs):** user-visible library changes also require updates in sibling `../FE-Docs/` (see [workflows.md](workflows.md)). That is separate from OKF sync—do both when both apply.

If unaffected, state why in the final response (`OKF unaffected (non-behavioral edit)` for pure comment/typo/format).

## Conflicts
1. Prefer source, tests, generated artifacts, lockfiles, manifests over OKF prose.
2. Fix OKF when wrong.
3. Mention the correction in the agent final response.

## Sources
- This repository’s `.okf/` set
- Skill/spec: OKF v0.1 (repo agent instructions)
