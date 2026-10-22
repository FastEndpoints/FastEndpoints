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
- AspVersioning remains net10-only: Asp.Versioning.OpenApi 10 requires Microsoft.OpenApi <3.0.0 and cannot share the net11 OpenAPI stack. Built-in FastEndpoints route versioning works on both TFMs.
- MCP SDK 2.x uses stateless HTTP transport by default; existing list/call handler wiring remains. Caller context and tool visibility must be resolved per request, not per session (`Src/Agents/Mcp/Extensions.cs`).
- OpenAPI media/response compatibility adapters: [openapi.md](openapi.md#tags-security-and-framework-compatibility).
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## Sources
- `global.json`
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/**/*.csproj`
