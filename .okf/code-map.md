---
type: Reference
title: Code Map
description: Repository layout, important source/test locations, generated outputs, and edit guidance.
tags: [code-map, navigation]
---

# Code Map

## Top-level layout

| Path | Purpose |
| --- | --- |
| `README.md` | Public project summary and docs site link. |
| `FastEndpoints.slnx` | Main solution for packages, harnesses, benchmarks, and most tests. |
| `NativeAot.slnx` | Native AOT harness/test solution. |
| `Directory.Packages.props` | Central NuGet package versions and compatibility comments. |
| `Src/` | Product source packages. |
| `Tests/` | Unit, integration, and native AOT test projects. |
| `TestHarness/` | ASP.NET sample/harness apps and native AOT checker used by tests. |
| `Benchmark/` | BenchmarkDotNet and load-test projects. |
| `.github/workflows/publish-to-nuget.yml` | GitHub release workflow. |
| `azure-pipeline.yml` | Azure test pipeline. |
| `clean.sh` | Deletes `bin` and `obj` directories. |

## Source package map

| Path | Purpose |
| --- | --- |
| `Src/Library/` | Main `FastEndpoints` package. Key areas: `Endpoint/`, `Binder/`, `Validation/`, `Auth/`, `Middleware/`, `Messaging/`, `Testing/`, `X402/`. |
| `Src/Core/` | Shared service resolver and assembly scanner infrastructure. |
| `Src/Attributes/` | Attribute package used by runtime/generator/consumers. |
| `Src/Messaging/` | Messaging packages split into core, local messaging, remote RPC, and remote test helpers. |
| `Src/JobQueues/` | Command-backed job queue package. |
| `Src/CommandRules/` | Command rule dispatching package. |
| `Src/OpenApi/` | Microsoft OpenAPI integration, schemas, operations, validation processors, export target. |
| `Src/OpenApi.Kiota/` | Kiota client generation support for new OpenAPI path. |
| `Src/Swagger/` | Legacy NSwag Swagger integration and MSBuild export target. |
| `Src/ClientGen*/` | Legacy NSwag/Kiota client generators. |
| `Src/Generator/` | Roslyn generator package and analyzer release tracking files. |
| `Src/Generator.Cli/` | CLI for serializer-context generation. |
| `Src/Agents/` | MCP and A2A add-on packages; not currently included as active projects in `FastEndpoints.slnx`. |

## Tests and harnesses

| Path | Purpose |
| --- | --- |
| `Tests/UnitTests/FastEndpoints/` | Main framework unit tests. |
| `Tests/UnitTests/FastEndpoints.Swagger/` | Legacy Swagger unit tests; solution entries are currently commented. |
| `Tests/UnitTests/FastEndpoints.Testing/` | Testing helper unit tests. |
| `Tests/IntegrationTests/FastEndpoints/` | Main integration tests using `TestHarness/Web/`. |
| `Tests/IntegrationTests/FastEndpoints.*` | Integration tests for Agents, OData, OpenAPI, OpenAPI.Kiota, and legacy Swagger. |
| `Tests/NativeAotTests/` | Native AOT checker tests. |
| `TestHarness/Web/` | Broad ASP.NET harness with feature folders under `[Features]/`. |
| `TestHarness/Sandbox/` | Sandbox source/contracts/tests solution. |
| `TestHarness/NativeAotChecker/` | Native AOT validation app. |

## Generated and build output guidance

- Do not edit `bin/` or `obj/`; they are ignored build outputs.
- `Src/*/Generated/` directories exist for generated source but may be empty in the repo.
- Serializer context generation defaults to `Generated/FastEndpoints` and may produce `SerializerContexts.g.cs`, `SerializerContextExtensions.g.cs`, and `.fastendpoints-generator-cache`.
- OpenAPI/Swagger targets generate module initializer files under intermediate output paths (`obj/`) and export docs under `wwwroot/openapi` during configured AOT publish workflows.
- `TestHarness/NativeAotChecker/Generated/` is ignored by `.gitignore` and should be treated as generated output unless a task explicitly targets generation behavior.

## Sources

- `FastEndpoints.slnx`
- `NativeAot.slnx`
- `.gitignore`
- `Src/Generator.Cli/README.md`
- `Src/Generator/FastEndpoints.Generator.targets`
- `Src/OpenApi/FastEndpoints.OpenApi.targets`
- `Src/Swagger/FastEndpoints.Swagger.targets`
