---
type: Reference
title: Code Map
description: Top-level layout and where endpoints, packages, tests, and generators live.
tags: [layout]
---

# Code Map

## Layout

| Path | Purpose |
| --- | --- |
| `Src/` | All shippable library packages |
| `Src/Library/` | Main `FastEndpoints` package (HTTP REPR runtime) |
| `Src/Core/` | `FastEndpoints.Core` (service resolver, assembly scanner) |
| `Src/Attributes/` | Attributes + small contracts for generators/consumers |
| `Src/Messaging/` | Messaging.Core, Messaging, Messaging.Remote*, testing helpers |
| `Src/JobQueues/` | Job queue package (`IJobStorageRecord` addons, `JobQueueOptions`, providers SPI, `DuplicateJobException`) |
| `Src/CommandRules/` | Rule-based command dispatch package |
| `Src/Security/` | JWT/cookie/refresh/revocation |
| `Src/OpenApi/`, `Src/OpenApi.Kiota/` | OpenAPI + Kiota client gen |
| `Src/Swagger/`, `Src/ClientGen*` | Legacy NSwag-era packages (slnx “Legacy” folder) |
| `Src/Generator/`, `Src/Generator.Cli/` | Roslyn generators + CLI |
| `Src/Testing/` | `FastEndpoints.Testing` |
| `Src/Agents/` | Mcp / A2A addons + Shared linked sources |
| `Src/*/Generated/` | Often empty placeholders or build outputs; prefer not hand-edit |
| `TestHarness/` | Sample apps: `Web`, `OData`, `OpenApi.Kiota`, `Sandbox`, `NativeAotChecker` |
| `Tests/UnitTests/` | Unit tests (xunit v3) |
| `Tests/IntegrationTests/` | WAF/integration tests against harnesses |
| `Tests/NativeAotTests/` | AOT publish/check tests |
| `Benchmark/` | BenchmarkDotNet + load tests |
| `Directory.Packages.props` | Central NuGet versions |
| `Src/Directory.Build.props` | Shared version, TFMs, signing, package metadata |
| `FastEndpoints.slnx` | Primary solution |
| `NativeAot.slnx` | AOT-focused solution |
| `azure-pipeline.yml` | Azure DevOps test pipeline (tag-triggered pack path differs) |
| `.github/workflows/publish-to-nuget.yml` | Tag `v*` test → pack → push NuGet → GH release |
| `../FE-Docs/` (sibling) | Public docs site source; content under `src/content/docs/`; not in this solution |

## Modules (Library internals)

| Folder under `Src/Library/` | Concern |
| --- | --- |
| `Main/` | `AddFastEndpoints`, `UseFastEndpoints`, discovery, bootstrap |
| `Endpoint/` | Endpoint base, setup, send, validation, processors, mappers |
| `Binder/` | Request binding |
| `Config/` | Global `Config` options |
| `Validation/` | FluentValidation wrappers |
| `Messaging/` | Command handlers living in Library surface |
| `Middleware/` | e.g. antiforgery |
| `Auth/`, `X402/` | Auth hooks / payment protocol support |
| `Testing/` | Lightweight test helpers shipped with main package |

## Entry points
- **Consumer app:** `services.AddFastEndpoints(...)` then `app.UseFastEndpoints(...)` or `MapFastEndpoints`.
- **Test harness Web:** `TestHarness/Web/Program.cs`; full feature surface for integration tests.
- **Integration SUT fixture:** `Tests/IntegrationTests/FastEndpoints/Sut.cs` → `AppFixture<Web.Program>`.
- **Pack/publish:** `dotnet pack FastEndpoints.slnx -c Release`; push `Src/**/*.nupkg`.

## Feature samples
Harness features under `TestHarness/Web/[Features]/…` (e.g. `Admin/Login/Endpoint.cs`) show canonical endpoint layout: namespace-per-feature, `Endpoint : Endpoint<Request, Response>`, `Configure()` + handle methods.

## Generated code
See [generated-code.md](generated-code.md). Generators: discovered types, access-control constants, reflection cache, service registration, generic processor types; CLI emits STJ serializer contexts when `GenerateSerializerContexts=true`.

## Sources
- `FastEndpoints.slnx`
- `Src/Library/`
- `TestHarness/Web/Program.cs`
- `Tests/IntegrationTests/FastEndpoints/Sut.cs`
