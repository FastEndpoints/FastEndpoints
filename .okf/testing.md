---
type: Reference
title: Testing
description: Test layout, frameworks, commands, fixtures, and validation expectations.
tags: [testing, validation]
---

# Testing

## Frameworks

- xUnit v3 is the test framework.
- Shouldly is the main assertion library.
- FakeItEasy is available for tests and analyzer coverage.
- ASP.NET integration tests use Microsoft.AspNetCore.Mvc.Testing/TestHost through project references and `FastEndpoints.Testing`.

## Test layout

| Path | Purpose |
| --- | --- |
| `Tests/UnitTests/FastEndpoints/` | Unit coverage for core framework behavior, queues, messaging, binding, route prefixes, service resolver, X402, warmup, generators. |
| `Tests/IntegrationTests/FastEndpoints/` | Main integration coverage against `TestHarness/Web/`. |
| `Tests/IntegrationTests/FastEndpoints.OpenApi/` | New OpenAPI integration tests. |
| `Tests/IntegrationTests/FastEndpoints.OpenApi.Kiota/` | Kiota/OpenAPI integration tests. |
| `Tests/IntegrationTests/FastEndpoints.OData/` | OData integration tests. |
| `Tests/IntegrationTests/FastEndpoints.Agents/` | MCP/A2A integration tests. |
| `Tests/IntegrationTests/FastEndpoints.Swagger/` | Legacy Swagger integration tests; currently commented out of main solution. |
| `Tests/UnitTests/FastEndpoints.Swagger/` | Legacy Swagger unit tests; currently commented out of main solution. |
| `Tests/UnitTests/FastEndpoints.Testing/` | Testing helper tests. |
| `Tests/NativeAotTests/` | Native AOT validation tests. |
| `TestHarness/Web/` | Broad ASP.NET harness and feature test endpoints. |

## Commands

Main release-style validation:

```bash
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes"
```

Targeted examples:

```bash
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj
dotnet test NativeAot.slnx -c Release
```

## xUnit runner behavior

- `Tests/Directory.Build.props` sets shared test properties and references xUnit v3, Shouldly, FakeItEasy, Microsoft.NET.Test.Sdk, and the Visual Studio runner.
- Integration xUnit runner configs commonly set `parallelizeTestCollections` to `false`.
- Azure CI overwrites `Tests/IntegrationTests/FastEndpoints/xunit.runner.json` to disable assembly and collection parallelization before running tests.

## What to test

- Binding, validation, serialization, endpoint setup DSL, processor ordering/state, auth/security metadata, and response behavior need integration coverage when behavior is user-visible.
- Package-specific changes should target that package's unit/integration project and any harness app it depends on.
- API or package dependency changes should validate the main solution and, when relevant, `NativeAot.slnx`.
- Generator changes should cover Roslyn/source-generation behavior and serializer context CLI/MSBuild paths where practical.

## Sources

- `Tests/Directory.Build.props`
- `Tests/IntegrationTests/FastEndpoints/xunit.runner.json`
- `FastEndpoints.slnx`
- `NativeAot.slnx`
- `azure-pipeline.yml`
- `.github/workflows/publish-to-nuget.yml`
