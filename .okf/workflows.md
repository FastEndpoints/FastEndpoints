---
type: Playbook
title: Workflows
description: Build, test, pack, publish, changelog, and FE-Docs commands for the FastEndpoints monorepo.
tags: [build]
---

# Workflows

## Setup
- SDK and runtime requirements: [dependencies.md](dependencies.md#runtime). CI installation differences are described under Pack and publish below.
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
dotnet run --project TestHarness/Web/Web.csproj --framework net10.0

# Optional destructive cleanup, from the repository root:
# recursively deletes all bin/obj directories beneath the current directory
./clean.sh
```

Sandbox: `TestHarness/Sandbox/Sandbox.slnx` for isolated experiments.

## Test
Commands, MTP runner, CI filter, and the Kiota skip: [testing.md](testing.md).

## Pack and publish
NuGet-only release (no long-running service). Changelog for GH releases: `Src/Library/changelog.md`.

```bash
dotnet pack FastEndpoints.slnx -c Release
dotnet nuget push "Src/**/*.nupkg" -k <NUGET_API_KEY> -s https://api.nuget.org/v3/index.json --skip-duplicate
```

Release automation is defined in [publish-to-nuget.yml](../.github/workflows/publish-to-nuget.yml) (tags `v*`). It uses secretless OIDC for a short-lived NuGet key. The nuget.org username is `djnitehawk`, not GitHub `dj-nitehawk`; no long-lived API key secret is needed.

Coverage gaps: GitHub tests both TFMs but its Native AOT steps are disabled. Kiota integration is omitted under `CI`/`TF_BUILD`. Azure installs only the SDK from `global.json` and tests net11, including AOT through its project glob; it does not test net10 or pack/push. Commands and manual coverage: [testing.md](testing.md).

Changed Agents addons must receive a new independent version in their `.csproj` before release; `--skip-duplicate` skips updated assemblies packaged under an already-published version.

Job permissions: `id-token: write` (OIDC), `contents: write` (GH release).

Trusted publishing policy on nuget.org must match: owner `FastEndpoints`, repo `FastEndpoints`, workflow file `publish-to-nuget.yml` (filename only).

## Changelog
Rolling current-cycle release notes: `Src/Library/changelog.md`. Non-beta tags dump the **entire file** as the GitHub release body. Entries are purged at every release, so this is not a permanent history or Keep-a-Changelog file. No version headers. During ordinary changes, preserve the sponsorship banner and unrelated entries; do not purge entries unless explicitly asked as part of release work.

The update gate is in [AGENTS.md](../AGENTS.md#before-finishing). FE-Docs-only edits do not need a changelog entry. Do not duplicate an existing `<details>` for the same change.

Prepend a new `<details>` immediately under the matching heading (newest first). Do not add headings.

- `## New 🎉`
- `## Fixes 🪲`
- `## Improvements 🚀`
- `## Minor Breaking Changes ⚠️`

Use HTML `<details><summary>user-facing title</summary>` plus short consumer-facing prose and an optional small code sample. The HTML comment near the top of the file is the entry template. Summaries state consumer impact, not the implementation.

**Voice and detail:** Write short, conversational entries in the maintainer's voice, speaking directly to users. Focus on their needs and the practical impact, not a technical dump. Prefer plain language over API inventories and implementation details. Keep useful code examples, but leave detailed setup, storage contracts, response-code matrices, and operational edge cases to the public documentation. Add a documentation pointer where appropriate. Retain essential compatibility, safety, and migration warnings. These rules stand on their own; do not rely on temporary changelog entries as tone references.

- **New:** Introduce the capability, who it helps, and why. A useful shape is: "If you're building [use case], you can now [capability]. [Brief benefit or relevant compatibility note.]" Follow with an optional example and documentation pointer, not a mandatory formula.
- **Fixes:** Describe the user-visible problem and what now works correctly.
- **Improvements:** State what is better and the practical benefit.
- **Breaking changes:** Say what changed, who is affected, and what users must do to migrate. Include a small before/after example when useful. Give users enough detail to act safely, while keeping the same conversational voice.

## Lint and format
- Style primarily via `.editorconfig` + ReSharper/Rider DotSettings (`FastEndpoints.sln.DotSettings.user` is user-local).
- No dedicated `dotnet format` script required by CI from inspected files; follow editorconfig when editing.

## Codegen and migrations
- Roslyn generators, serializer contexts, and OpenAPI export: [generated-code.md](generated-code.md).

## Public documentation
User-facing docs are the sibling `../FE-Docs/` SvelteKit site (`src/content/docs/` numbered topics). Published: https://fast-endpoints.com. Preview: https://dev.fastendpoints-doc-site.pages.dev. Setup commands are defined by [`package.json`](../../FE-Docs/package.json) and [`README.md`](../../FE-Docs/README.md).

The update gate is in [AGENTS.md](../AGENTS.md#before-finishing). Match neighboring page style. Docs are not built by this repo's solutions or publish workflow.

```bash
# from ../FE-Docs/
npm install   # first time
npm run dev
npm run build
```

## Sources
- `.github/workflows/publish-to-nuget.yml`
- `Src/Library/changelog.md`
- `azure-pipeline.yml`
- `clean.sh`
