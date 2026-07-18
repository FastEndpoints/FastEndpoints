---
type: Playbook
title: Workflows
description: Build, test, pack, clean, and publish commands for the FastEndpoints monorepo.
tags: [build]
---

# Workflows

## Setup
- Install .NET SDKs matching CI: **8.x, 9.x, 10.x** (tests default to **net10.0** via `Tests/Directory.Build.props`).
- Clone repo; restore via normal `dotnet` on solutions (central package management).
- No docker-compose required for core unit/integration suite (in-process WAF).
- Signing key files `FastEndpoints.snk` / public key present in repo for signed builds.

## Build and run
```bash
# Primary solution
dotnet build FastEndpoints.slnx -c Release

# AOT solution
dotnet build NativeAot.slnx -c Release

# Sample harness (dev)
dotnet run --project TestHarness/Web/Web.csproj

# Clean bin/obj
./clean.sh
```

Sandbox: `TestHarness/Sandbox/Sandbox.slnx` for isolated experiments.

## Test
See [testing.md](testing.md). Common:

```bash
dotnet test FastEndpoints.slnx -c Release --filter "ExcludeInCiCd!=Yes"
dotnet test Tests/**/*.csproj --filter "ExcludeInCiCd!=Yes"   # Azure pipeline style
```

## Pack and publish
```bash
dotnet pack FastEndpoints.slnx -c Release
dotnet nuget push "Src/**/*.nupkg" -k <NUGET_API_KEY> -s https://api.nuget.org/v3/index.json
```

GitHub Actions (`.github/workflows/publish-to-nuget.yml`): on tag `v*`:
1. setup SDKs 8/9/10
2. `dotnet test FastEndpoints.slnx -c Release --filter ExcludeInCiCd!=Yes`
3. pack + push with `secrets.NUGET_API_KEY`
4. non-beta tags: GH release body from `Src/Library/changelog.md`

Azure `azure-pipeline.yml`: tag `v*` trigger; runs tests under `Tests/` with same filter (pack steps not in the snippet beyond test — verify file when changing release process).

## Lint and format
- Style primarily via `.editorconfig` + ReSharper/Rider DotSettings (`FastEndpoints.sln.DotSettings.user` is user-local).
- No dedicated `dotnet format` script required by CI from inspected files; follow editorconfig when editing.

## Codegen and migrations
- **Roslyn generators:** reference `FastEndpoints.Generator` as analyzer (`OutputItemType=Analyzer` in project refs).
- **Serializer contexts (AOT):** set `GenerateSerializerContexts=true` (optional `SerializerContextOutputPath`, `GeneratorCliVersion`). Targets in `Src/Generator/FastEndpoints.Generator.targets` run CLI before compile.
- **OpenAPI export (harness/AOT):** `ExportOpenApiDocs` (.json) and/or `ExportHttpFiles` (.http) via `FastEndpoints.OpenApi.targets` (see NativeAotChecker). One JIT build + one process when both props are true; one `ExportOpenApiArtifactsAndExitAsync` (or either legacy `Export*AndExitAsync` alias) exports every CLI-requested format and exits.
- No DB migrations in-repo.

## Public documentation
User-facing docs live in a **sibling repo**, not this monorepo:

| Item | Location |
| --- | --- |
| Docs source | `../FE-Docs/src/content/docs/` (numbered topic pages) |
| Site | SvelteKit app in `../FE-Docs/` |
| Published | https://fast-endpoints.com |
| Dev preview | https://dev.fastendpoints-doc-site.pages.dev |

**When library work finishes**, update FE-Docs if the change is user-visible: public APIs, config knobs, endpoint/messaging/job/security/OpenAPI/AOT behavior, breaking changes, or new features. Prefer editing the matching topic under `src/content/docs/` (and related nav if structure changes). Match the **writing style and formatting** of existing content on that page and in neighboring topics (tone, structure, headings, code samples, callouts). Do **not** paste full doc pages into OKF.

Local docs site (from `../FE-Docs/`):
```bash
npm install   # first time
npm run dev   # vite dev
npm run build
```

Docs are not built/tested by `FastEndpoints.slnx` or NuGet publish workflows.

## Env vars / secrets (names only)
| Name | Use |
| --- | --- |
| `NUGET_AUTH_TOKEN` / `NUGET_API_KEY` | NuGet push (CI secret) |
| App config keys e.g. `TokenKey` | Harness JWT signing (configuration, not committed secrets) |

## Sources
- `.github/workflows/publish-to-nuget.yml`
- `azure-pipeline.yml`
- `clean.sh`
- `Src/Generator/FastEndpoints.Generator.targets`
- `Tests/Directory.Build.props`
- `../FE-Docs/README.md`
- `../FE-Docs/package.json`
