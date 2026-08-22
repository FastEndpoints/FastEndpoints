---
type: Playbook
title: Workflows
description: Build, test, pack, publish, changelog, and FE-Docs commands for the FastEndpoints monorepo.
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
See [testing.md](testing.md). Root `global.json` selects the Microsoft Testing Platform runner for `dotnet test` (SDK 10 + xunit.v3 4). Common:

```bash
dotnet test FastEndpoints.slnx -c Release --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1
dotnet test Tests/**/*.csproj --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1   # Azure pipeline style
```

## Pack and publish
NuGet-only release (no long-running service). Changelog for GH releases: `Src/Library/changelog.md`.

```bash
dotnet pack FastEndpoints.slnx -c Release
dotnet nuget push "Src/**/*.nupkg" -k <NUGET_API_KEY> -s https://api.nuget.org/v3/index.json
```

GitHub Actions (`.github/workflows/publish-to-nuget.yml`): on tag `v*`:
1. setup SDKs 8/9/10
2. `dotnet test FastEndpoints.slnx -c Release --filter ExcludeInCiCd!=Yes --max-parallel-test-modules 1` (`Int.OpenApi.Kiota` is omitted via `IsTestingPlatformApplication=false` when `CI`/`TF_BUILD` is set)
3. pack
4. `NuGet/login@v1` exchanges GitHub OIDC for a short-lived nuget.org API key (`user: djnitehawk` in the workflow; this is the nuget.org username, not GitHub `dj-nitehawk`)
5. push with that temp key and `--skip-duplicate` (trusted publishing; no long-lived API key secret). Needed because independently versioned Agents packages (`FastEndpoints.Mcp` / `FastEndpoints.A2A`) are packed with the solution and already exist on nuget.org when their version is unchanged.
6. non-beta tags: GH release body from `Src/Library/changelog.md`. AOT test step is commented; do not assume an AOT gate.

Job permissions: `id-token: write` (OIDC), `contents: write` (GH release).

Trusted publishing policy on nuget.org must match: owner `FastEndpoints`, repo `FastEndpoints`, workflow file `publish-to-nuget.yml` (filename only).

Azure `azure-pipeline.yml`: tag `v*` trigger; tests under `Tests/` with the same filter. No pack/push in that file.

## Changelog
Rolling current-cycle release notes: `Src/Library/changelog.md`. Non-beta tags dump the **entire file** as the GitHub release body. Not Keep-a-Changelog: no version headers, do not reset or rewrite the file, keep the sponsorship banner and existing entries.

Update it in the same change as user-visible library work (new public API/package/feature, user-visible bug fix, notable perf/behavior improvement, breaking change). Skip tests-only, OKF, comments, CI, formatting, internal refactors with no consumer effect, and FE-Docs-only edits. Do not duplicate an existing `<details>` for the same change.

Prepend a new `<details>` immediately under the matching heading (newest first). Do not add headings.

- `## New 🎉`
- `## Fixes 🪲`
- `## Improvements 🚀`
- `## Minor Breaking Changes ⚠️`

Match neighbors: HTML `<details><summary>user-facing title</summary>` plus short consumer-facing prose and an optional small code sample. The HTML comment near the top of the file is the entry template. Summaries state consumer impact, not the implementation. Breaking entries must say what broke and how to migrate.

Changelog is release notes. FE-Docs is API docs. Do both when both apply.

## Lint and format
- Style primarily via `.editorconfig` + ReSharper/Rider DotSettings (`FastEndpoints.sln.DotSettings.user` is user-local).
- No dedicated `dotnet format` script required by CI from inspected files; follow editorconfig when editing.

## Codegen and migrations
- **Roslyn generators:** reference `FastEndpoints.Generator` as analyzer (`OutputItemType=Analyzer` in project refs).
- **Serializer contexts (AOT):** set `GenerateSerializerContexts=true` (optional `SerializerContextOutputPath`, `GeneratorCliVersion`). Targets in `Src/Generator/FastEndpoints.Generator.targets` run CLI before compile.
- **OpenAPI export (harness/AOT):** `ExportOpenApiDocs` (.json) and/or `ExportHttpFiles` (.http) via `FastEndpoints.OpenApi.targets` (see NativeAotChecker). One JIT build + one process when both props are true; one `ExportOpenApiArtifactsAndExitAsync` (or either legacy `Export*AndExitAsync` alias) exports every CLI-requested format and exits.
- No DB migrations in-repo.

## Public documentation
User-facing docs are the sibling `../FE-Docs/` SvelteKit site (`src/content/docs/` numbered topics). Published: https://fast-endpoints.com. Preview: https://dev.fastendpoints-doc-site.pages.dev.

Update FE-Docs when a change is user-visible (public APIs, config, endpoint/messaging/job/security/OpenAPI/AOT behavior, breaking changes, new features). Match neighboring page style. Do not paste doc pages into OKF. Docs are not built by this repo's solutions or publish workflow. Same class of change also needs a changelog entry (see Changelog).

```bash
# from ../FE-Docs/
npm install   # first time
npm run dev
npm run build
```

## Env vars / secrets (names only)
| Name | Use |
| --- | --- |
| App config keys e.g. `TokenKey` | Harness JWT signing (samples only) |

## Sources
- `.github/workflows/publish-to-nuget.yml`
- `Src/Library/changelog.md`
- `azure-pipeline.yml`
- `clean.sh`
- `Src/Generator/FastEndpoints.Generator.targets`
- `Tests/Directory.Build.props`
- `../FE-Docs/README.md`
- `../FE-Docs/package.json`
