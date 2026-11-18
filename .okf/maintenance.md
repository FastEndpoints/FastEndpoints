---
type: Reference
title: Maintenance
description: How to keep the OKF set conformant and when to update it.
tags: [maintain]
---

# Maintenance

## Conformance
- Format, gates, and content discipline: [AGENTS.md](../AGENTS.md). The okf skill supplies the closed type list; use `Reference` if unsure.
- No secrets in OKF. Prefer `## Sources` for multi-source claims.
- Topic ownership: core files summarize boundaries; testing maps regression suites; gotchas preserves traps and owner-approved review exclusions. Detailed domain contracts belong in the task-specific topics linked from [index.md](index.md#task-specific).

## Update triggers
Evaluate OKF when changes hit:
- architecture / package boundaries / dependency directions
- public APIs, endpoint pipeline contracts, messaging/job contracts
- Agents friend-assembly surface or call-site changes: update the binary-contract stock in [agents.md](agents.md).
- persistence SPIs (e.g. job storage interfaces)
- deps / TFMs / central package management
- build, test, pack, publish, codegen commands
- testing strategy or harness layout
- security/auth surfaces (JWT/cookies/permissions/feature flags; no separate `security.md`)
- config/env/ops assumptions
- conventions, layout, gotchas
- docs maintenance workflow / FE-Docs paths (not the full docs body)
- changelog path/format / GH-release notes workflow (not the entry bodies)
