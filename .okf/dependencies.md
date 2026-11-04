---
type: Reference
title: Dependencies
description: Runtimes, central package management, and key libraries with compatibility notes.
tags: [deps]
---

# Dependencies

## Runtime
- **Language:** C# (LangVersion latest)
- **TFMs (libraries):** net10.0; net11.0 (default from `Src/Directory.Build.props`, including OpenApi, OpenApi.Kiota and Agents)
- **Overrides:** Generator → netstandard2.0; Generator.Cli → net10.0 with major runtime roll-forward; Attributes → netstandard2.0 + net10/11; AspVersioning and its unit tests → net10.0 only. The netstandard targets support analyzer hosts, not older application runtimes.
- **SDK:** pinned in `global.json` with latest-patch roll-forward. Install the .NET 10 SDK alongside it for the dual-runtime test matrix. net11 ASP.NET JwtBearer/TestHost/Mvc.Testing package versions are selected in `Directory.Packages.props`.
- **Tests / integration and AOT harnesses:** net10.0; net11.0. Benchmarks and standalone Sandbox projects remain net10.0.
- **ASP.NET:** `FrameworkReference` Microsoft.AspNetCore.App where needed

## Packages
- **Central versions:** root `Directory.Packages.props` (`ManagePackageVersionsCentrally`)
- **Do not** duplicate `PackageVersion` in leaf projects; use `PackageReference` Include only
- NuGet audit mode: `direct`

## Constraints
- `Microsoft.CodeAnalysis.CSharp` is pinned to **[5.0.0]**; minimum analyzer host is .NET 10 SDK / Visual Studio 2026 18.0. Kiota builder is pinned to **[1.29.2]** on net10 for OpenAPI 2.x compatibility; net11 uses the OpenAPI 3.x-compatible line. Bumping either needs explicit compatibility validation (comments and versions in `Directory.Packages.props`).
- TFM-conditional package versions for JwtBearer, TestHost, Mvc.Testing, Microsoft.AspNetCore.OpenApi and Kiota (10/11 groups). OpenAPI 10 uses Microsoft.OpenApi 2.x; OpenAPI 11 uses 3.x.
- AspVersioning remains net10-only: Asp.Versioning.OpenApi 10 requires Microsoft.OpenApi <3.0.0 and cannot share the net11 OpenAPI stack. Built-in FastEndpoints route versioning works on both TFMs. Migration gate and checklist: [below](#aspversioning-net11-migration).
- MCP SDK 2.x uses stateless HTTP transport by default; existing list/call handler wiring remains. Caller context and tool visibility must be resolved per request, not per session (`Src/Agents/Mcp/Extensions.cs`).
- OpenAPI media/response compatibility adapters: [openapi.md](openapi.md#tags-security-and-framework-compatibility).
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## AspVersioning net11 migration

Keep the integration and its unit tests net10-only until a released upstream package supports the net11 OpenAPI stack. The current Asp.Versioning.OpenApi 10.2.3 dependency range is Microsoft.OpenApi `>= 2.7.5 && < 3.0.0`; this repository's ASP.NET Core OpenAPI 11 RC3 requires `>= 3.10.2 && < 4.0.0`. A net10 package being consumable from net11 does not establish dependency or runtime compatibility. Do not force an incompatible Microsoft.OpenApi version or suppress restore warnings to bypass this gate.

Upstream investigation starting points (recheck status at migration time): [package dependencies/releases](https://www.nuget.org/packages/Asp.Versioning.OpenApi), [reflection-free prototype PR #1186](https://github.com/dotnet/aspnet-api-versioning/pull/1186), and [framework extensibility proposal #66725](https://github.com/dotnet/aspnetcore/issues/66725). The prototype and framework work are not proof that a compatible versioning package has shipped; no release date is assumed.

When upstream compatibility is available:

1. Inspect the candidate package's actual dependency ranges and release/migration notes against the repository's then-current net10/net11 OpenAPI versions. Check both TFMs; retain separate conditional versions if the upstream major versions require them.
2. Update the Asp.Versioning package entries in `Directory.Packages.props`, making compatible entries available for net11 as well as net10. Keep versions centralized and preserve compatible Http, Mvc.ApiExplorer, and OpenApi combinations.
3. Restore dual targeting in `Src/AspVersioning/FastEndpoints.AspVersioning.csproj` and `Tests/UnitTests/FastEndpoints.AspVersioning/Unit.AspVersioning.csproj`. Remove obsolete compatibility comments only once the blocker is resolved. Review the integration against upstream API changes, especially OpenAPI registration, per-version document discovery, and reflection workarounds; do not assume a TFM-only change is enough.
4. Restore/build the solution, then run `Unit.AspVersioning.csproj` explicitly with `--framework net10.0` and `--framework net11.0`. Validate versioned routing and actual per-version document generation on both stacks; add focused host coverage if existing tests do not exercise the new integration. Follow [testing.md](testing.md) for the broader dual-framework matrix, OpenAPI regression suites, host isolation, and snapshot regeneration. Built-in route-versioning snapshots alone do not prove Asp.Versioning integration compatibility. If AOT support is claimed, validate the published versioned OpenAPI path too.
5. Inspect a locally packed integration package's target assets/dependencies without publishing. Update public FE-Docs and the changelog per [workflows.md](workflows.md), then remove the resolved net10-only guidance from this file, `code-map.md`, and `monorepo-packages.md`. Replace this temporary checklist with any remaining durable compatibility constraints.

## Sources
- `global.json`
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/**/*.csproj`
