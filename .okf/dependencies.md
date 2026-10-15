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
- **Overrides:** Generator → netstandard2.0; Generator.Cli → net10.0 with major runtime roll-forward; Attributes → netstandard2.0 + net10/11. The netstandard targets support analyzer hosts, not older application runtimes.
- **SDK:** pinned in `global.json` to 11.0.100-rc.2.26504.105 with latest-patch roll-forward. net11 ASP.NET JwtBearer/TestHost/Mvc.Testing packages use the matching RC2.
- **Tests / harnesses:** net10.0
- **ASP.NET:** `FrameworkReference` Microsoft.AspNetCore.App where needed

## Packages
- **Central versions:** root `Directory.Packages.props` (`ManagePackageVersionsCentrally`)
- **Do not** duplicate `PackageVersion` in leaf projects; use `PackageReference` Include only
- NuGet audit mode: `direct`

## Key libraries
| Library | Role |
| --- | --- |
| FluentValidation | Request validators |
| Microsoft.AspNetCore.Authentication.JwtBearer | Security package (version per TFM) |
| Microsoft.AspNetCore.OpenApi / Scalar.AspNetCore | OpenAPI docs (modern path) |
| NSwag.* | Legacy Swagger/ClientGen (deprecate note in Directory.Packages.props) |
| Grpc.AspNetCore.Server / Grpc.Net.Client* | Remote messaging |
| MessagePack | Default RPC marshalling |
| protobuf-net / Grpc.Reflection | Protobuf wire format + gRPC reflection (Messaging.Remote.Reflection only) |
| Microsoft.AspNetCore.OData | OData package |
| ModelContextProtocol.AspNetCore | Agents MCP; v2.x targets the 2026-07-28 MCP spec (stateless-by-default transport, backward compatible with the addon's v1-style handler wiring) |
| Microsoft.CodeAnalysis.CSharp **[4.11.0]** | Generators; retains existing Roslyn analyzer-host compatibility |
| Microsoft.OpenApi.Kiota.Builder **[1.29.2]** | Pinned; Kiota OpenAPI 3.x vs AspNetCore.OpenApi 10's 2.x mismatch |
| xunit.v3, Shouldly, FakeItEasy, Bogus | Test stack |
| BenchmarkDotNet, NBomber | Benchmarks / load |

## Constraints
- Bump of `Microsoft.CodeAnalysis.CSharp` or Kiota builder needs explicit compatibility validation (comments in `Directory.Packages.props`).
- TFM-conditional package versions for JwtBearer, TestHost and Mvc.Testing (10/11 groups); Asp.Versioning 10 is shared by both TFMs.
- Keep Microsoft.AspNetCore.OpenApi 10 on both TFMs until Asp.Versioning.OpenApi supports Microsoft.OpenApi 3.x. OpenAPI 11 requires 3.x but Asp.Versioning.OpenApi 10 requires <3.0.0. Coordinate that migration with the Kiota pin.
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## Sources
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/**/*.csproj`
