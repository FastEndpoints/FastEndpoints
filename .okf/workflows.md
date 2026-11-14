---
type: Playbook
title: Workflows
description: Build, test, pack, publish, changelog, and FE-Docs commands for the FastEndpoints monorepo.
tags: [build]
---

# Workflows

## Setup
- SDK and runtime requirements: [dependencies.md](dependencies.md#runtime). CI installation differences are described under Pack and publish below.

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
NuGet release automation and trusted publishing are defined in [publish-to-nuget.yml](../.github/workflows/publish-to-nuget.yml) (tags `v*`).

```bash
dotnet pack FastEndpoints.slnx -c Release
```

Publishing uses secretless OIDC for a short-lived NuGet key. The nuget.org username is `djnitehawk`, not GitHub `dj-nitehawk`; no long-lived API key secret is needed. Push with `--skip-duplicate` because unchanged independently versioned packages may already be published.

Coverage gaps: GitHub tests both TFMs but its Native AOT steps are disabled. Kiota integration is omitted under `CI`/`TF_BUILD`. Azure installs only the SDK from `global.json` and tests net11, including AOT through its project glob; it does not test net10 or pack/push. Commands and manual coverage: [testing.md](testing.md).

Changed Agents addons must receive a new independent version in their `.csproj` before release; `--skip-duplicate` skips updated assemblies packaged under an already-published version.

Trusted publishing policy on nuget.org must match: owner `FastEndpoints`, repo `FastEndpoints`, workflow file `publish-to-nuget.yml` (filename only).

## Changelog
Rolling current-cycle release notes: `Src/Library/changelog.md`. Non-beta tags dump the **entire file** as the GitHub release body. Entries are purged at every release, so this is not a permanent history or Keep-a-Changelog file. No version headers. During ordinary changes, preserve the sponsorship banner and unrelated entries; do not purge entries unless explicitly asked as part of release work.

The update gate is in [AGENTS.md](../AGENTS.md#before-finishing). FE-Docs-only edits do not need a changelog entry. Do not duplicate an existing `<details>` for the same change.

Prepend a new `<details>` under the matching existing heading (newest first); do not add headings. Follow the HTML comment template near the top of the file. Summaries state consumer impact, not implementation.

**Voice and detail:** Write short, conversational entries in the maintainer's voice, speaking directly to users. Focus on their needs and the practical impact, not a technical dump. Prefer plain language over API inventories and implementation details. Keep useful code examples, but leave detailed setup, storage contracts, response-code matrices, and operational edge cases to the public documentation. Add a documentation pointer where appropriate. Retain essential compatibility, safety, and migration warnings. These rules stand on their own; do not rely on temporary changelog entries as tone references.

- New features explain who benefits and why; fixes/improvements explain the user-visible change. Breaking changes must identify affected users and migration steps, with a before/after example when useful.

## Lint and format
- Style primarily via `.editorconfig` + ReSharper/Rider DotSettings (`FastEndpoints.sln.DotSettings.user` is user-local).

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
