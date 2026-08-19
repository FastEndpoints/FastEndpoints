---
type: Architecture
title: Architecture
description: REPR endpoint pipeline, package graph, messaging, auth, and discovery invariants.
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
| `FastEndpoints.Core` | Service resolution (`IServiceResolver`, `ServiceResolverClient` shared resolve façade), assembly scanning |
| `FastEndpoints.Messaging.Core` | `ICommand` / `IEvent` / handler interfaces |
| `FastEndpoints.Messaging` | In-process command/event bus |
| `FastEndpoints.JobQueues` | Background jobs over commands + storage SPI |
| `FastEndpoints` (Library) | HTTP endpoints, binding, validation, middleware, config |
| `FastEndpoints.Security` | JWT bearer helpers, cookies, refresh/revocation |
| `FastEndpoints.OpenApi` | Microsoft.AspNetCore.OpenApi document pipeline |
| `FastEndpoints.Generator` | Roslyn generators (discovered types, ACL, reflection cache, service registration, generic processors) |
| `FastEndpoints.Generator.Cli` | Build-time JSON serializer context generation |
| `FastEndpoints.Testing` | `AppFixture`, collection fixtures, WAF cache for integration tests |
| Messaging.Remote* | gRPC RPC for remote command/event execution (MessagePack by default; wire format pluggable) |

**Request path (simplified):** `AddFastEndpoints` registers discovery data → `UseFastEndpoints`/`MapFastEndpoints` maps routes → `FeRequestHandler` resolves endpoint instance → bind → validate → pre-processors → (`ResponseStarted` short-circuit) → `OnBeforeHandle` → optional `SkipHandlerIfResponseStarted` short-circuit → `HandleAsync`/`ExecuteAsync` → post-processors → send response.

**Startup/mapping split (`Src/Library/Main/`):** public facades stay on `MainExtensions` (`AddFastEndpoints` / `UseFastEndpoints` / `MapFastEndpoints`, plus internal `BuildRoute` for OpenApi/Agents friend usage). Mapping orchestration is `EndpointRouteMapper`; auth policy materialization is `EndpointSecurityPolicies`; accepts/produces API explorer defaults are `EndpointProducesMetadata`; binder/validator precompile is `EndpointWarmup`. Request execution remains `FeRequestHandler` → `EndpointBootstrap` → `Endpoint.ExecAsync`.

**Discovery ownership:** `AddFastEndpoints` resolves the type list once (`EndpointData.DiscoverTypes` for reflection, or source-generated `DiscoveredTypes`). `EndpointData` builds the HTTP endpoint definition catalog only (`Found`). Messaging handler registration is owned by `MessagingExtensions.RegisterHandlers` into `CommandHandlerRegistry` (same path used by standalone `AddMessaging`). Skip `AddMessaging` when `AddFastEndpoints` already ran; both must not invent a second registration owner.

## Dependency rules
- **Allowed:** higher packages reference lower foundation packages (`Attributes`, `Core`, `Messaging.Core`).
- **Library** references Attributes, JobQueues, Messaging (not Security/OpenApi; those are optional consumer packages).
- **Security/OpenApi/OData/AspVersioning** reference Library (addons on top of core HTTP).
- **Generator** references Attributes only (analyzer package); consumers reference Generator as analyzer.
- **Agents** (`Mcp`, `A2A`) reference Library; share internal types via linked `Src/Agents/Shared/*.cs` (not a separate NuGet).
- **Agents friend internals:** Library grants `InternalsVisibleTo` to `FastEndpoints.Mcp` / `FastEndpoints.A2A` (`Src/Library/Metadata.cs`). Consumed internals are a binary contract across independently versioned packages; stock in [gotchas.md](gotchas.md).
- **Forbidden for agents:** invent reverse deps (e.g. Core → Library) or ship Agents.Shared as a public package unless code changes deliberately.

## Communication
- **HTTP:** endpoints mapped into ASP.NET routing; config via `UseFastEndpoints(c => …)` (`Config` / `Cfg`).
- **In-process messaging:** command/event/stream handlers registered via `MessagingExtensions.RegisterHandlers` (from `AddMessaging` or as a side path of `AddFastEndpoints`), or DI/test helpers.
- **Remote:** gRPC handler server (`AddHandlerServer` / remote client connection). The wire format is chosen by an
  `IRpcMarshallerFactory` and defaults to MessagePack. `AddHandlerServer(marshaller:)` sets it server-side;
  `RemoteConnection.MarshallerFactory` sets it per client connection. Both sides also take the bound gRPC method name from
  the factory, so they always agree (MessagePack keeps the historical empty name).
- **Remote reflection:** `FastEndpoints.Messaging.Remote.Reflection` is an opt-in satellite package holding the protobuf wire
  format and gRPC server reflection (`AddHandlerReflection` / `MapHandlerReflection`). It generates Google.Protobuf descriptors
  from the command CLR types, so protobuf/reflection dependencies stay out of `Messaging.Remote`.
- **Jobs:** `AddJobQueues<TJob, TStorage>()`; storage provider is app-supplied. Optional business-key idempotency via `JobQueueOptions.IdempotencyKeyFor<TCommand>(Func<TCommand,string?>)` + storage record `IHasIdempotencyKey` + provider uniqueness / `DuplicateJobException`.

## Persistence
- Framework does **not** own an app DB. Job queues require consumer `IJobStorageProvider` / `IJobStorageRecord` implementations.
- Job idempotency is storage-enforced on `(QueueID, IdempotencyKey)` while the row exists (including completed); not filtered to incomplete-only.
- No EF/migrations in this repo.

## Security / auth
- Auth is ASP.NET Core middleware + optional `FastEndpoints.Security` (`Src/Security`): JWT bearer, cookies, refresh, revocation.
- Endpoint `Configure()`: `AllowAnonymous()`, roles/permissions/policies; `AccessControl(...)` can emit constants via Generator. Global options: `Config.Security`.
- Feature flags: implement `IFeatureFlag`, call `FeatureFlag<T>()` to disable an endpoint at runtime.
- Harness wires `AddAuthenticationJwtBearer`, `AdminOnly`, `UseJwtRevocation<T>()`, `UseAntiforgeryFE`. Sample JWT keys are test-only.
- Remote messaging is trusted-network RPC unless the consumer adds auth.
- Publish is secretless OIDC ([workflows.md](workflows.md)).

## Invariants
1. Endpoint types implement `IEndpoint`; public base is `Endpoint<TRequest[, TResponse]>`.
2. AOT: do **not** rely on reflection discovery; use `AddFastEndpoints(DiscoveredTypes.All)` (+ generator).
3. Mappers/validators discovered types are typically treated as singletons for performance; no per-request state in mappers.
4. Shared library TFMs: **net8.0;net9.0;net10.0** (exceptions: Generator netstandard2.0; Attributes multi-TFM; Agents often net9+net10).
5. Strong-name signing via `FastEndpoints.snk` (public key in Directory.Build.props / InternalsVisibleTo).
6. Central package versions: root `Directory.Packages.props` (`ManagePackageVersionsCentrally`).
7. Agents addons version **independently** of core (`Src/Agents/Directory.Build.props` imports parent then overrides).
8. Do not rename, retype, or remove Library internals listed in the Agents friend-assembly stock ([gotchas.md](gotchas.md)) without checking published agent package compatibility and restocking OKF.

## Sources
- `Src/Library/Main/MainExtensions.cs`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Src/Library/Endpoint/Endpoint.cs`
- `Src/Library/Metadata.cs`
- `Src/Security/`
- `Src/Agents/Directory.Build.props`
