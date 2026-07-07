---
okf_version: "0.1"
---

# OKF Knowledge Set

This directory contains compact operational knowledge for agents working in FastEndpoints. Read relevant files before editing, and keep OKF synchronized with source, tests, manifests, and repo docs.

## Core reading order

- [Project Overview](project-overview.md) - Project purpose, scope, package families, and glossary.
- [Architecture](architecture.md) - Major components, dependency rules, runtime model, and invariants.
- [Code Map](code-map.md) - Repository layout and where to add or validate behavior.
- [Conventions](conventions.md) - Coding, endpoint, package, and test conventions.

## Workflow and validation

- [Workflows](workflows.md) - Build, test, pack, generation, cleanup, and release workflows.
- [Testing](testing.md) - Test projects, fixtures, commands, and validation expectations.

## Task-specific references

- [Dependencies](dependencies.md) - Target frameworks, central package versions, and key compatibility constraints.
- [Operations](operations.md) - CI/release, NuGet publishing, benchmarks, and runtime harness notes.
- [Gotchas](gotchas.md) - Practical traps and non-obvious constraints.
- [Maintenance](maintenance.md) - Rules for keeping OKF conformant and current.

## Authority rule

If OKF conflicts with source code, tests, generated artifacts, or project manifests, verify current behavior from those authoritative sources, then update OKF.

## Maintenance rule

Before finishing work, update OKF if the change affects architecture, behavior, commands, dependencies, tests, deployment, or conventions. If no update is needed, state why in the final response.
