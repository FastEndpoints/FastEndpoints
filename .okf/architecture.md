---
type: Architecture
title: Architecture
description: REPR endpoint pipeline, multi-package dependency graph, messaging, and discovery invariants.
tags: [architecture]
---

# Architecture

## Style
- **Library monorepo** of ASP.NET Core packages (not a multi-service deployable).
- **REPR**: one endpoint class owns Configure + Handle for a request (and optional response) DTO.
- Vertical-slice friendly: features group Request/Endpoint/Validator/Mapper; no MVC controllers required.
- Dual registration modes: **reflection scan** (dev default) vs **source-generated type lists** (AOT / trimmed).

## Components

```
Attributes / Messaging.Core
        │
        ▼
      Core  ◄── Messaging  ◄── JobQueues / CommandRules
        │
        ▼
    Library (FastEndpoints) ──► Security, OpenApi, OData, AspVersioning, HealthChecks, Agents.*
        │
        ▼
    Generator (analyzer) + Generator.Cli (serializer contexts)
```

| Layer | Role |
| --- | --- |
| `FastEndpoints.Attributes` | Shared attributes/contracts; multi-TFM including netstandard2.0 for generator |
| `FastEndpoints.Core` | Service resolution, assembly scanning shared plumbing |
| `FastEndpoints.Messaging.Core` | `ICommand` / `IEvent` / handler interfaces |
| `FastEndpoints.Messaging` | In-process command/event bus |
| `FastEndpoints.JobQueues` | Background jobs over commands + storage SPI |
| `FastEndpoints` (Library) | HTTP endpoints, binding, validation, middleware, config |
| `FastEndpoints.Security` | JWT bearer helpers, cookies, refresh/revocation |
| `FastEndpoints.OpenApi` | Microsoft.AspNetCore.OpenApi document pipeline |
| `FastEndpoints.Generator` | Roslyn generators (discovered types, ACL, reflection cache, service registration, generic processors) |
| `FastEndpoints.Generator.Cli` | Build-time JSON serializer context generation |
| `FastEndpoints.Testing` | `AppFixture`, collection fixtures, WAF cache for integration tests |
| Messaging.Remote* | gRPC RPC for remote command/event execution (MessagePack) |

**Request path (simplified):** `AddFastEndpoints` registers discovery data → `UseFastEndpoints`/`MapFastEndpoints` maps routes → `FeRequestHandler` resolves endpoint instance → bind → validate → pre-processors → `HandleAsync` → post-processors → send response.

## Dependency rules
- **Allowed:** higher packages reference lower foundation packages (`Attributes`, `Core`, `Messaging.Core`).
- **Library** references Attributes, JobQueues, Messaging (not Security/OpenApi — those are optional consumer packages).
- **Security/OpenApi/OData/AspVersioning** reference Library (addons on top of core HTTP).
- **Generator** references Attributes only (analyzer package); consumers reference Generator as analyzer.
- **Agents** (`Mcp`, `A2A`) reference Library; share internal types via linked `Src/Agents/Shared/*.cs` (not a separate NuGet).
- **Forbidden for agents:** invent reverse deps (e.g. Core → Library) or ship Agents.Shared as a public package unless code changes deliberately.

## Communication
- **HTTP:** endpoints mapped into ASP.NET routing; config via `UseFastEndpoints(c => …)` (`Config` / `Cfg`).
- **In-process messaging:** command/event handlers registered from discovery or DI helpers.
- **Remote:** gRPC handler server (`AddHandlerServer` / remote client connection) with MessagePack marshalling.
- **Jobs:** `AddJobQueues<TJob, TStorage>()`; storage provider is app-supplied.

## Persistence
- Framework does **not** own an app DB. Job queues require consumer `IJobStorageProvider` / `IJobStorageRecord` implementations.
- No EF/migrations in this repo.

## Security / auth (boundary summary)
- Auth is ASP.NET Core middleware + `FastEndpoints.Security` helpers.
- Endpoint-level `Permissions` / `Roles` / `AccessControl` (generator can emit permission constants).
- See [security.md](security.md) for operational detail.

## Invariants
1. Endpoint types implement `IEndpoint`; public base is `Endpoint<TRequest[, TResponse]>`.
2. AOT: do **not** rely on reflection discovery — use `AddFastEndpoints(DiscoveredTypes.All)` (+ generator).
3. Mappers/validators discovered types are typically treated as singletons for performance — no per-request state in mappers.
4. Shared library TFMs: **net8.0;net9.0;net10.0** (exceptions: Generator netstandard2.0; Attributes multi-TFM; Agents often net9+net10).
5. Strong-name signing via `FastEndpoints.snk` (public key in Directory.Build.props / InternalsVisibleTo).
6. Central package versions: root `Directory.Packages.props` (`ManagePackageVersionsCentrally`).
7. Agents addons version **independently** of core (`Src/Agents/Directory.Build.props` imports parent then overrides).

## Sources
- `Src/Library/Main/MainExtensions.cs`
- `Src/Library/Endpoint/Endpoint.cs`
- `Src/Library/FastEndpoints.csproj`
- `Src/Generator/DiscoveredTypesGenerator.cs`
- `Src/Agents/Directory.Build.props`
