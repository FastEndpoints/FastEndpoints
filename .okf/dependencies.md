---
type: Reference
title: Dependencies
description: Runtime versions, package management, important libraries, and compatibility constraints.
tags: [dependencies, packages]
---

# Dependencies

## Runtime and language

- Product source packages use `TargetFrameworks` `net8.0;net9.0;net10.0` from `Src/Directory.Build.props` unless a project overrides it.
- Tests and benchmarks target `net10.0` through their `Directory.Build.props` files.
- `Src/Generator/FastEndpoints.Generator.csproj` overrides multi-targeting and targets `netstandard2.0` for analyzer/source-generator packaging compatibility.
- `LangVersion` is `latest`, nullable is enabled, and implicit usings are enabled in shared props.

## Package management

- Central package management is enabled in `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`.
- Add/update NuGet versions in `Directory.Packages.props`, not individual `.csproj` files, unless a package-specific reason exists.
- NuGet audit mode is direct-only.

## Key libraries

| Area | Packages |
| --- | --- |
| ASP.NET framework | `Microsoft.AspNetCore.App` framework reference, ASP.NET auth/test/openapi packages by target framework. |
| Validation | `FluentValidation`. |
| Testing | `xunit.v3`, `Shouldly`, `FakeItEasy`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.TestHost`. |
| OpenAPI | `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi.Kiota.Builder`, `Scalar.AspNetCore`. |
| Legacy Swagger/client generation | `NSwag.*` packages. |
| Messaging/RPC | `Grpc.*`, `MessagePack`. |
| Agents | `ModelContextProtocol.AspNetCore`. |
| Bench/load | `BenchmarkDotNet`, `NBomber`. |
| Packaging/source link | `Microsoft.SourceLink.GitHub`. |

## Compatibility notes

- `Microsoft.CodeAnalysis.CSharp` is pinned to `[4.11.0]` with an inline comment: not upgradeable for net8 compatibility.
- `Microsoft.OpenApi.Kiota.Builder` is pinned to `[1.29.0]` because newer Kiota uses Microsoft.OpenApi 3.x while `Microsoft.AspNetCore.OpenApi` still uses 2.x.
- NSwag packages are grouped with a comment to deprecate at the next FastEndpoints major version jump.
- Target-framework-conditioned package versions exist for ASP.NET authentication, MVC testing, TestHost, and Asp.Versioning packages across `net8.0`, `net9.0`, and `net10.0`.
- Strong-name signing uses repo `.snk` files through props; preserve signing behavior for packages/tests.

## Sources

- `Directory.Packages.props`
- `Src/Directory.Build.props`
- `Tests/Directory.Build.props`
- `Benchmark/Directory.Build.props`
- `Src/Generator/FastEndpoints.Generator.csproj`
- `Src/**/*.csproj`
