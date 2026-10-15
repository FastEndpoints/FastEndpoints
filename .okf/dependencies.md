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
- **SDK:** pinned in `global.json` to 11.0.100-rc.2.26504.105 with latest-patch roll-forward. net11 ASP.NET JwtBearer/TestHost/Mvc.Testing packages use the matching RC2.
- **Tests / integration and AOT harnesses:** net10.0; net11.0. Benchmarks and standalone Sandbox projects remain net10.0.
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
| Microsoft.CodeAnalysis.CSharp **[5.0.0]** | Generators; minimum analyzer host is .NET 10 SDK / Visual Studio 2026 18.0 |
| Microsoft.OpenApi.Kiota.Builder | net10: pinned **[1.29.2]** for OpenAPI 2.x; net11: **1.36.1** for OpenAPI 3.x |
| xunit.v3, Shouldly, FakeItEasy, Bogus | Test stack |
| BenchmarkDotNet, NBomber | Benchmarks / load |

## Constraints
- Bump of `Microsoft.CodeAnalysis.CSharp` or Kiota builder needs explicit compatibility validation (comments in `Directory.Packages.props`).
- TFM-conditional package versions for JwtBearer, TestHost, Mvc.Testing, Microsoft.AspNetCore.OpenApi and Kiota (10/11 groups). OpenAPI 10 uses Microsoft.OpenApi 2.x; OpenAPI 11 uses 3.x.
- AspVersioning remains net10-only: Asp.Versioning.OpenApi 10 requires Microsoft.OpenApi <3.0.0 and cannot share the net11 OpenAPI stack. Built-in FastEndpoints route versioning works on both TFMs.
- `OpenApiMediaTypeValue` aliases the concrete 2.x media type on net10 and `IOpenApiMediaType` on net11. Mutations use `GetMutableMediaType(s)` to localize referenced media types before changing schemas/examples. XML schema signatures use `NodeType` on net11 instead of 2.x's `Attribute`/`Wrapped`.
- On net11, `OpenApiDocument` registers `ResponseTypeCompatibilityProvider` once to preserve last-declared response-type selection, its media formats and distinct default responses.
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## Sources
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/**/*.csproj`
