---
type: Reference
title: Maintenance
description: How to keep the OKF set conformant and when to update it.
tags: [maintain]
---

# Maintenance

## Conformance
- Format and day-to-day gates: [AGENTS.md](../AGENTS.md). The okf-setup skill supplies the closed type list; use `Reference` if unsure.
- No secrets in OKF. Prefer `## Sources` for multi-source claims.
- Optimize reading cost, not line count. Keep invariants and failure consequences, not exhaustive API/test catalogs or implementation walkthroughs.
- Keep each detailed fact in one topic owner and link from other files. Core files summarize boundaries; testing maps regression suites; gotchas preserves traps and owner-approved review exclusions. Detailed contracts belong in [openapi.md](openapi.md) and [remote-events.md](remote-events.md); link there instead of repeating implementation rules in test inventories.

## Update triggers
Sync OKF when changes hit:
- architecture / package boundaries / dependency directions
- public APIs, endpoint pipeline contracts, messaging/job contracts
- **Agents friend-assembly surface:** Library `internal` members consumed by `FastEndpoints.Mcp` / `FastEndpoints.A2A` via `InternalsVisibleTo` (stock in [gotchas.md](gotchas.md); summary in [monorepo-packages.md](monorepo-packages.md)). Restock when agents gain/drop internal core calls, or when those core members change signature/shape.
- persistence SPIs (e.g. job storage interfaces)
- deps / TFMs / central package management
- build, test, pack, publish, codegen commands
- testing strategy or harness layout
- security/auth surfaces (JWT/cookies/permissions/feature flags; no separate `security.md`)
- config/env/ops assumptions
- conventions, layout, gotchas
- docs maintenance workflow / FE-Docs paths (not the full docs body)
- changelog path/format / GH-release notes workflow (not the entry bodies)

Finish gates, including public docs/changelog and explicit unaffected reporting, are owned by `AGENTS.md`. Commands and entry format: [workflows.md](workflows.md).

## Sources
- This repository’s `.okf/` set
- Skill/spec: OKF v0.1 (repo agent instructions)
