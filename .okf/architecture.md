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
- **Remote event queues:** default delivery is server-streaming `sub`. A hub provider that implements
  `IEventHubDeliveryAck<T>` binds duplex `sub-ack` instead and marks the row complete only after the subscriber stores the
  hub `TrackingID`. `sub` stays the default. The ACK is the inbox write, not handler success.
  `IEventHubDeliveryAck<T>.DeliveryAckTimeout` defaults to 30 seconds and is capped by the row's remaining replay lifetime.
  Delivery writes have a separate budget using the same timeout, also capped by remaining replay lifetime. The
  dispatcher directly awaits cancellation-aware writes and reads, including cancellation cleanup before ownership release. Timeout closes the call with `DeadlineExceeded`, leaves
  the durable row pending, and releases exclusive subscriber ownership. Traps: [gotchas.md](gotchas.md).
- **Remote event connection ownership:** each server dispatcher acquires its connection and releases it in `finally`. `EventDeliveryAckDispatcher` owns exclusive acquisition, connected/rejected logging, and `FailedPrecondition` rejection. `EventHub.OnDeliveryAck` reads and validates the hello, then delegates. ACK ownership spans timeout validation and awaited stream cancellation cleanup.
- **Remote event worker ownership:** `HubContext` owns pending-batch query construction, materialization and retrieval retries, plus shared hub completion retries, deserialization and idle signal waiting for ordinary and ACK dispatchers. `GetNextNonEmptyBatch` owns the fetch/idle loop, returning a non-empty batch or null on linked cancellation (including disposed-signal shutdown). Retrieval error counts are local to each fetch and reset after success; dispatchers retain their batch limits and deserialization cancellation handling. Client adapters compose `EventSubscriberRuntime<,,,>` for purge setup, handler dependencies, the shared receiver/executor semaphore, and dependency assembly for both receiver and executor launch. The ACK dispatcher assumes durable, non-destructive storage: transport failures leave rows pending naturally, and its completion overload forwards to the shared durable retry path. Ordinary dispatch retains mode-sensitive completion. `EventDispatcherWorker.RequeueBatchAsync` owns best-effort in-memory suffix recovery, with explicit caller tokens for deserialization cancellation (`CancellationToken.None`) and stream failure (linked connection/app token). `EventDeliveryAckDispatcher.GetPhaseTimeout` owns the non-negative replay-lifetime cap, calculated afresh at each write/read phase.
- **Remote subscriber supervision and retention:** `EventReceiveSupervisor<TEvent>` owns client reconnect delay, receive error counts/callback isolation, cancellation and terminal logging. Protocol-local sessions dispose calls before reconnect delay; ordinary initial call creation still propagates failures, ordinary replacement creation is terminal, and ACK call creation is retryable. Receivers retain persistence, hello/ACK ordering and fatal metadata validation. `EventSubscriberRetentionPolicy<TStorageRecord>` owns ACK record capability validation, clock-skew allowance validation/defaults, retained-key deadline calculation, and the side-effect-free purge expression. ACK adapters validate during construction and receivers read the allowance again at startup; `EventSubscriberStorage<,>` retains provider setup and hourly worker lifetime. Adapter identity/logger categories and separate adapter storage caches remain protocol-specific.
- **Remote reflection:** `FastEndpoints.Messaging.Remote.Reflection` is an opt-in satellite package holding the protobuf wire
  format and gRPC server reflection (`AddHandlerReflection` / `MapHandlerReflection`). It generates Google.Protobuf descriptors
  from the command CLR types, so protobuf/reflection dependencies stay out of `Messaging.Remote`.
- **Jobs:** `AddJobQueues<TJob, TStorage>()` with an app-supplied storage provider. Optional business-key idempotency via `JobQueueOptions.IdempotencyKeyFor`. AOT construction and dedupe traps: [gotchas.md](gotchas.md).
- **HTTP idempotency:** fingerprint mode is an output-cache policy (`AddIdempotency` + `Idempotency()`). Financial mode is a separate reservation pipeline (`AddFinancialIdempotency` + `UseFinancialIdempotency` + `FinancialIdempotency()`) with `IFinancialIdempotencyStore` (atomic `TryBegin`, ownership-token settlement). Requires a stable caller scope. Do not combine both on one endpoint. Not shared with job-queue `IdempotencyKeyFor`. Settlement and identity traps: [gotchas.md](gotchas.md).

## Persistence
- Framework does **not** own an app DB. Job queues require consumer `IJobStorageProvider` / `IJobStorageRecord` implementations.
- Job-queue idempotency rules: [gotchas.md](gotchas.md).
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
4. TFMs, central package versions, and strong-name signing: [dependencies.md](dependencies.md).
5. Agents addons version independently of core: [monorepo-packages.md](monorepo-packages.md).
6. Do not rename, retype, or remove Library internals in the Agents friend-assembly stock ([gotchas.md](gotchas.md)) without checking published agent packages and restocking that list.

## Sources
- `Src/Library/Main/MainExtensions.cs`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Src/Library/Endpoint/Endpoint.cs`
- `Src/Library/Metadata.cs`
- `Src/Security/`
- `Src/Agents/Directory.Build.props`
