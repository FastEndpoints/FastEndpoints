---
type: Playbook
title: Operations
description: Release, packaging, and CI operations for publishing FastEndpoints NuGet packages.
tags: [ops]
---

# Operations

## Deploy model
- **Artifact:** NuGet packages from `Src/**` projects (`dotnet pack`).
- **Not deployed as** a long-running service from this repo.
- **Channels:** nuget.org push; GitHub Release (non-beta tags) with body from `Src/Library/changelog.md`.

## CI / release
| Pipeline | Trigger | Role |
| --- | --- | --- |
| `.github/workflows/publish-to-nuget.yml` | push tag `v*` | test → pack → nuget push → optional GH release |
| `azure-pipeline.yml` | tags `v*` (branches excluded) | UseDotNet 10.x; adjust xunit runner; `dotnet test` under `Tests/` |

GitHub workflow installs SDKs 8/9/10, runs filtered tests on `FastEndpoints.slnx`, packs solution, pushes `Src/**/*.nupkg` using `NUGET_API_KEY` secret.

## Services and ports
- No production services. Local harnesses are Kestrel web apps (`TestHarness/Web`, etc.); default ASP.NET ports when run.
- Remote messaging tests may spin gRPC handler server in-process via harness (`AddHandlerServer`).

## Data stores
- None owned by the library. Job queue storage is consumer-implemented.

## Config and observability
- Package metadata: ProjectUrl `https://fast-endpoints.com/`, MIT license, SourceLink.
- Changelog for releases: `Src/Library/changelog.md`.
- Secrets: only CI NuGet API key name documented; never commit values.
- Harness configuration files: `TestHarness/Web/appsettings*.json` (keys only; treat as samples).

## Caveats
- Agents packages may be omitted from main slnx (commented) while still present in tree; pack inventory follows solution inclusion.
- AOT test step in publish workflow may be commented; do not assume AOT gate on every release without checking workflow.
- Beta tags skip GitHub Release creation (`!contains(github.ref, 'beta')`).

## Sources
- `.github/workflows/publish-to-nuget.yml`
- `azure-pipeline.yml`
- `Src/Directory.Build.props`
- `Src/Library/changelog.md`
