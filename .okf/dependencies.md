---
type: Reference
title: Dependencies
description: Runtimes, central package management, and key libraries with compatibility notes.
tags: [deps]
---

# Dependencies

## Runtime
- **Language:** C# (LangVersion latest)
- **TFMs (libraries):** net8.0; net9.0; net10.0 (default from `Src/Directory.Build.props`)
- **Exceptions:** Generator → netstandard2.0; Attributes → netstandard2.0 + net8/9/10; Agents Mcp/A2A → net9.0;net10.0
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
| MessagePack | RPC marshalling |
| Microsoft.AspNetCore.OData | OData package |
| ModelContextProtocol.AspNetCore | Agents MCP |
| Microsoft.CodeAnalysis.CSharp **[4.11.0]** | Generators — pinned for net8 compatibility (comment in props) |
| Microsoft.OpenApi.Kiota.Builder **[1.29.0]** | Pinned — Kiota OpenAPI 3.x vs AspNetCore.OpenApi 2.x mismatch |
| xunit.v3, Shouldly, FakeItEasy, Bogus | Test stack |
| BenchmarkDotNet, NBomber | Benchmarks / load |

## Constraints
- Bump of `Microsoft.CodeAnalysis.CSharp` or Kiota builder needs explicit compatibility validation (comments in `Directory.Packages.props`).
- TFM-conditional package versions for JwtBearer, TestHost, Mvc.Testing, Asp.Versioning (8/9/10 groups).
- Library version single-sourced in `Src/Directory.Build.props`; Agents override `Version` per csproj.
- Strong-name key required for pack/sign; InternalsVisibleTo entries must use the public key.

## Sources
- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/Generator/FastEndpoints.Generator.csproj`
