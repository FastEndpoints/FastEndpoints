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
* Bug hunts and reviews: check [accepted policies and review exclusions](gotchas.md#accepted-policies-and-review-exclusions) before ranking findings.
* [Workflows](workflows.md): build, pack, changelog, publish
* [Testing](testing.md): unit/integration/AOT, filters, harnesses

## Task-specific
* [Dependencies](dependencies.md) · [Gotchas](gotchas.md) · [Maintenance](maintenance.md)
* [Monorepo Packages](monorepo-packages.md) · [Generated Code](generated-code.md)
* [Remote Events](remote-events.md): protocol/storage contracts, worker ownership, reliability and test isolation

## Authority
If OKF conflicts with source, tests, generated artifacts, or manifests: verify those, then update OKF.

## Maintenance
Normative OKF use/update gates: repo canonical agent instructions (`AGENTS.md`). Reminder + conformance detail: [Maintenance](maintenance.md).
Expanded library-monorepo set. Runtime/ops guidance is covered by architecture and workflows; a separate operations file is unnecessary.
