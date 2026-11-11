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
- **SDK:** stable .NET 11 pinned in `global.json` with latest-patch roll-forward and prereleases disabled. Install the .NET 10 SDK alongside it for the dual-runtime test matrix. Framework-specific package versions are selected in `Directory.Packages.props`.
- **Tests / integration and AOT harnesses:** net10.0; net11.0. Benchmarks and standalone Sandbox projects remain net10.0.
- **ASP.NET:** `FrameworkReference` Microsoft.AspNetCore.App where needed

## Packages
- **Central versions:** root `Directory.Packages.props` (`ManagePackageVersionsCentrally`)
- **Do not** duplicate `PackageVersion` in leaf projects; use `PackageReference` Include only
- NuGet audit mode: `direct`

## Constraints
- `Microsoft.CodeAnalysis.CSharp` is pinned to **[5.0.0]**; minimum analyzer host is .NET 10 SDK / Visual Studio 2026 18.0. Kiota builder is pinned to **[1.29.2]** on net10 for OpenAPI 2.x compatibility; net11 uses the OpenAPI 3.x-compatible line. Bumping either needs explicit compatibility validation (comments and versions in `Directory.Packages.props`).
- TFM-conditional package versions for JwtBearer, TestHost, Mvc.Testing, JsonPatch, EF Core, Microsoft.Extensions, Microsoft.AspNetCore.OpenApi and Kiota (10/11 groups). The netstandard analyzer targets keep the .NET 10 Microsoft.Extensions baseline. OpenAPI 10 uses Microsoft.OpenApi 2.x; OpenAPI 11 uses 3.x.
- AspVersioning remains net10-only: Asp.Versioning.OpenApi 10 requires Microsoft.OpenApi <3.0.0 and cannot share the net11 OpenAPI stack. Built-in FastEndpoints route versioning works on both TFMs. Migration gate and verification: [below](#aspversioning-net11-migration).
- MCP SDK 2.x uses stateless HTTP transport by default; existing list/call handler wiring remains. Caller context and tool visibility must be resolved per request, not per session (`Src/Agents/Mcp/Extensions.cs`).
- Scalar UI upgrades are not blocked by the package-DTO serializer tests: those tests use FluentValidation `ValidationFailure`, not Scalar DTOs (`TestHarness/NativeAotChecker/Endpoints/SerializerCtxGen/PackageDtoEndpoints.cs`). This does not establish Native AOT serialization support for Scalar DTOs; validate that separately if introducing them as endpoint contracts.
- OpenAPI media/response compatibility adapters: [openapi.md](openapi.md#tags-security-and-framework-compatibility).
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## Kiota 1.29.2 CVE warnings

`CVE-2026-105796` / `GHSA-rm89-rhwj-9j92` (Java/PHP doc-comment code injection) is fixed in the net10-pinned Kiota 1.29.2 security backport, as confirmed by the [release notes](https://github.com/microsoft/kiota/releases/tag/v1.29.2). The GitHub advisory still includes 1.29.2 in its affected range. [Advisory Database PR #10705](https://github.com/github/advisory-database/pull/10705), opened 2026-10-27 and still open when checked 2026-11-11, proposes excluding 1.29.2 for both `Microsoft.OpenApi.Kiota` and `Microsoft.OpenApi.Kiota.Builder`.

If this warning recurs, recheck the PR and [current advisory](https://github.com/advisories/GHSA-rm89-rhwj-9j92) before treating it as an unpatched dependency or changing the pin. Preserve the net10 OpenAPI 2.x compatibility constraint above. This correction applies only to this advisory, not all Kiota vulnerabilities; assess other CVEs independently. It does not authorize blanket audit suppression.

`CVE-2026-105795` / [GHSA-6gw6-rv2g-25mg](https://github.com/advisories/GHSA-6gw6-rv2g-25mg) is a separate low-severity warning affecting 1.29.2: unsafe `oauth_card_path` references in generated API plugin manifests. Its fix requires Kiota 1.35.0+, incompatible with the net10 OpenAPI stack. Until a compatible fix ships, use trusted descriptions and validate plugin manifest references before deployment. Do not suppress this warning as part of the Java/PHP advisory correction.

## AspVersioning net11 migration

Keep the integration and its unit tests net10-only until a released upstream package supports the net11 OpenAPI stack. The current Asp.Versioning.OpenApi 10.2.3 dependency range is Microsoft.OpenApi `>= 2.7.5 && < 3.0.0`; ASP.NET Core OpenAPI 11.0.0 requires `>= 3.11.0 && < 4.0.0`. A net10 package being consumable from net11 does not establish dependency or runtime compatibility. Do not force an incompatible Microsoft.OpenApi version or suppress restore warnings to bypass this gate.

Upstream investigation starting points (recheck status at migration time): [package dependencies/releases](https://www.nuget.org/packages/Asp.Versioning.OpenApi), [reflection-free prototype PR #1186](https://github.com/dotnet/aspnet-api-versioning/pull/1186), and [framework extensibility proposal #66725](https://github.com/dotnet/aspnetcore/issues/66725). The prototype and framework work are not proof that a compatible versioning package has shipped; no release date is assumed.

When restoring dual targeting in `Src/AspVersioning/FastEndpoints.AspVersioning.csproj` and `Tests/UnitTests/FastEndpoints.AspVersioning/Unit.AspVersioning.csproj`:
- Check released dependency ranges and API changes for compatible Http, Mvc.ApiExplorer, and OpenApi combinations on both TFMs; central conditional versions may still be needed.
- Run `Unit.AspVersioning.csproj` on net10/net11 and verify actual per-version document generation as well as routing. Built-in route-versioning snapshots alone are insufficient. Validate the published versioned OpenAPI path if claiming AOT support, and inspect locally packed target assets/dependencies without publishing.
- After verification, remove resolved net10-only guidance here and in the package map, plus the admonition under "Asp.Versioning.Http Package Support" in `../FE-Docs/src/content/docs/14-api-versioning.md` (FE-Docs `dev` branch). General test and release-documentation workflows remain in [testing.md](testing.md) and [workflows.md](workflows.md).

## Sources
- `global.json`
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/**/*.csproj`
