---
okf_version: "0.1"
---

# OKF Knowledge Set

Compact operational knowledge for agents working in the FastEndpoints repository. Read relevant files before editing. Keep synchronized with code, tests, docs, and configuration.

## Core reading order
* [Project Overview](project-overview.md): purpose, packages, status
* [Architecture](architecture.md): REPR model, package graph, invariants
* [Code Map](code-map.md): directories and where to edit
* [Conventions](conventions.md): naming, style, patterns

## Workflow and validation
* [Workflows](workflows.md): build, pack, clean, publish
* [Testing](testing.md): unit/integration/AOT, filters, harnesses

## Task-specific
* [Dependencies](dependencies.md) · [Operations](operations.md) · [Gotchas](gotchas.md) · [Maintenance](maintenance.md)
* [Monorepo Packages](monorepo-packages.md) · [Generated Code](generated-code.md) · [Security](security.md)

## Authority
If OKF conflicts with source, tests, generated artifacts, or manifests: verify those, then update OKF.

## Maintenance
Normative OKF use/update gates: repo canonical agent instructions (`AGENTS.md`). Reminder + conformance detail: [Maintenance](maintenance.md).
Before finishing, sync OKF when triggers apply; if not needed, state why (`OKF unaffected (non-behavioral edit)` for pure comment/typo/format).
