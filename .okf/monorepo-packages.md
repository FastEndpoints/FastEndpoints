---
type: Reference
title: Monorepo Packages
description: Inventory of FastEndpoints source packages, solutions, and independent version lines.
tags: [monorepo]
---

# Monorepo Packages

One root `.okf/` covers the whole repo. Packages are NuGet libraries, not separately agent-operated apps (no per-package OKF).

## Solutions
| Solution | Contents |
| --- | --- |
| `FastEndpoints.slnx` | Main libraries, Agents addons, tests, harnesses, benchmarks |
| `NativeAot.slnx` | HTTP and standalone messaging AOT checkers, their tests, library refs, and Sandbox contracts |
| `TestHarness/Sandbox/Sandbox.slnx` | Local sandbox |

## Package locations
Package and source locations: [code-map.md](code-map.md#layout). NuGet IDs normally follow `FastEndpoints.<Area>`; Library is `FastEndpoints`, and `Src/Agents/Mcp` / `Src/Agents/A2A` publish as `FastEndpoints.Mcp` / `FastEndpoints.A2A`. Exact package metadata lives in the project files.

## Version lines
- **Core line:** `Src/Directory.Build.props` `<Version>` (all standard Src packages unless overridden).
- **Agents line:** per-csproj `<Version>` under `Src/Agents/` (props file documents independence).

## Agents compatibility
Agents share linked sources and consume Library internals across independent version lines. Binary contract and restock rule: [agents.md](agents.md).

## Sources
- `FastEndpoints.slnx`
- `Src/Directory.Build.props`
- `Src/Agents/Directory.Build.props`
- `Src/Library/Metadata.cs`
- `Src/**/*.csproj`
