---
type: Reference
title: Monorepo Packages
description: Inventory of FastEndpoints source packages, solutions, and independent version lines.
tags: [monorepo]
---

# Monorepo Packages

One root `.okf/` covers the whole repo. Packages are NuGet libraries, not separately agent-operated apps (no per-package OKF).

## Solutions
| Solution | Contents |
| --- | --- |
| `FastEndpoints.slnx` | Main libraries, tests, harnesses, benchmarks; Agents folder often commented |
| `NativeAot.slnx` | AOT checker + subset of library refs + Sandbox contracts |
| `TestHarness/Sandbox/Sandbox.slnx` | Local sandbox |

## Package map (Src)

| Project path | NuGet-oriented name | Notes |
| --- | --- | --- |
| `Library/FastEndpoints.csproj` | FastEndpoints | Main HTTP framework |
| `Core/` | FastEndpoints.Core | DI/scanner core |
| `Attributes/` | FastEndpoints.Attributes | Multi-TFM attributes |
| `Messaging/Messaging.Core/` | FastEndpoints.Messaging.Core | Interfaces |
| `Messaging/Messaging/` | FastEndpoints.Messaging | In-proc bus |
| `Messaging/Messaging.Remote.Core/` | FastEndpoints.Messaging.Remote.Core | RPC core |
| `Messaging/Messaging.Remote/` | FastEndpoints.Messaging.Remote | gRPC server/client |
| `Messaging/Messaging.Remote.Reflection/` | FastEndpoints.Messaging.Remote.Reflection | Protobuf wire format + gRPC reflection |
| `Messaging/Messaging.Remote.Testing/` | FastEndpoints.Messaging.Remote.Testing | Test helpers |
| `JobQueues/` | FastEndpoints.JobQueues | Background jobs |
| `CommandRules/` | FastEndpoints.CommandRules | Rule dispatch |
| `Security/` | FastEndpoints.Security | JWT/cookies |
| `OpenApi/` | FastEndpoints.OpenApi | MS OpenAPI |
| `OpenApi.Kiota/` | FastEndpoints.OpenApi.Kiota | Kiota client gen |
| `Swagger/` | FastEndpoints.Swagger | Legacy NSwag |
| `ClientGen/` | FastEndpoints.ClientGen | Legacy clients |
| `ClientGen.Kiota/` | FastEndpoints.ClientGen.Kiota | Legacy Kiota |
| `Generator/` | FastEndpoints.Generator | Analyzers + targets |
| `Generator.Cli/` | FastEndpoints.Generator.Cli | `PackageId` explicit; tool |
| `Testing/` | FastEndpoints.Testing | Integration test base |
| `AspVersioning/` | FastEndpoints.AspVersioning | API versioning |
| `OData/` | FastEndpoints.OData | OData |
| `HealthChecks/` | FastEndpoints.HealthChecks | Probes |
| `Agents/Mcp/` | FastEndpoints.Mcp | Independent Version |
| `Agents/A2A/` | FastEndpoints.A2A | Independent Version |

## Version lines
- **Core line:** `Src/Directory.Build.props` `<Version>` (all standard Src packages unless overridden).
- **Agents line:** per-csproj `<Version>` under `Src/Agents/` (props file documents independence).

## Test / harness projects (non-pack or IsPackable false)
- `Tests/**`, `TestHarness/**`, `Benchmark/**` — development only.

## Sources
- `FastEndpoints.slnx`
- `Src/Directory.Build.props`
- `Src/Agents/Directory.Build.props`
- `Src/**/*.csproj`
