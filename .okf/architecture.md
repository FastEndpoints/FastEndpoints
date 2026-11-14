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

Project references (arrows point to dependencies):

```text
Library → Attributes, JobQueues, Messaging
JobQueues → Messaging → Core, Messaging.Core
CommandRules → JobQueues, Messaging, Messaging.Core
Security / OpenApi / OData / AspVersioning / Agents.* → Library
Messaging.Remote → Messaging.Remote.Core → Messaging.Core
Messaging.Remote.Reflection → Messaging.Remote
Generator → Attributes
```

`HealthChecks` and `OpenApi.Kiota` have no project reference to Library. `Generator` is consumed as an analyzer; `Generator.Cli` runs through MSBuild targets.

Package roles: [monorepo-packages.md](monorepo-packages.md). Source locations: [code-map.md](code-map.md).

**Request path (simplified):** `AddFastEndpoints` registers discovery data → `UseFastEndpoints`/`MapFastEndpoints` maps routes → `FeRequestHandler` (`ResponseStarted` short-circuit) resolves endpoint instance → bind → validate → pre-processors → (`ResponseStarted` short-circuit) → `OnBeforeHandle*` → optional `SkipHandlerIfResponseStarted` short-circuit → `HandleAsync`/`ExecuteAsync` → automatic response sending (unless disabled or already started) → `OnAfterHandle*`. Post-processors run in `ExecAsync`'s `finally`, including on its early-return and error paths; they do not precede automatic response sending.

**Startup/mapping split (`Src/Library/Main/`):** public facades stay on `MainExtensions` (`AddFastEndpoints` / `UseFastEndpoints` / `MapFastEndpoints`, plus internal `BuildRoute` for OpenApi/Agents friend usage). Mapping orchestration is `EndpointRouteMapper`; auth policy materialization is `EndpointSecurityPolicies`; accepts/produces API explorer defaults are `EndpointProducesMetadata`; binder/validator precompile is `EndpointWarmup`. Request execution remains `FeRequestHandler` → `EndpointBootstrap` → `Endpoint.ExecAsync`.

**Discovery ownership:** `AddFastEndpoints` resolves the type list once (`EndpointData.DiscoverTypes` for reflection, or source-generated `DiscoveredTypes`). `EndpointData` builds the HTTP endpoint definition catalog only (`Found`). `MessagingExtensions.RegisterHandlers` populates command and event catalogs for both full and standalone registration. Event ownership/configuration contracts: [messaging.md](messaging.md#in-process-event-ownership-and-inheritance). Skip `AddMessaging` when `AddFastEndpoints` already ran; do not invent a second registration owner.

## Dependency rules
- Preserve the dependency direction above; do not introduce reverse dependencies such as Core → Library.
- Agents share linked `Src/Agents/Shared/*.cs`, not a separate NuGet. Library friend internals are a binary contract across independently versioned addons: [agents.md](agents.md).
- OpenAPI validation has separate discovery, orchestration, application, child-scope and mutation owners: [openapi.md](openapi.md#generation-and-ownership).

## Communication
- **HTTP:** endpoints mapped into ASP.NET routing; config via `UseFastEndpoints(c => …)` (`Config` / `Cfg`).
- **In-process messaging:** command/event/stream handlers registered via `MessagingExtensions.RegisterHandlers` (from `AddMessaging` or as a side path of `AddFastEndpoints`), or DI/test helpers.
- **Remote:** gRPC handler server (`AddHandlerServer`) and client connections default to MessagePack. Wire-format selection and client/server compatibility: [messaging.md](messaging.md#remote-rpc-and-reflection).
- **Remote events:** ordinary server-streaming delivery and opt-in durable inbox ACK delivery. Protocol/storage contracts, worker ownership, deadlines, retention and isolation: [remote-events.md](remote-events.md).
- **Remote reflection:** the opt-in `Messaging.Remote.Reflection` satellite owns protobuf and gRPC server reflection, keeping those dependencies out of `Messaging.Remote`.
- **Jobs:** consumer-owned `IJobStorageProvider` / `IJobStorageRecord` persistence, not a framework-owned app DB. AOT construction and business-key dedupe contracts: [messaging.md](messaging.md#commands-and-jobs).
- **HTTP idempotency:** fingerprint mode is an output-cache policy (`AddIdempotency` + `Idempotency()`). Financial mode is a separate reservation pipeline (`AddFinancialIdempotency` + `UseFinancialIdempotency` + `FinancialIdempotency()`) with `IFinancialIdempotencyStore` (atomic `TryBegin`, ownership-token settlement). Requires a stable caller scope. Do not combine both on one endpoint. Not shared with job-queue `IdempotencyKeyFor`. Settlement and identity traps: [financial-idempotency.md](financial-idempotency.md).

## Security / auth
- Auth is ASP.NET Core middleware + optional `FastEndpoints.Security` (`Src/Security`): JWT bearer, cookies, refresh, revocation.
- Endpoint `Configure()`: `AllowAnonymous()`, roles/permissions/policies; `AccessControl(...)` can emit constants via Generator. Global options: `Config.Security`.
- Feature flags: implement `IFeatureFlag`, call `FeatureFlag<T>()` to disable an endpoint at runtime.
- Sample JWT keys are test-only.
- Remote messaging is trusted-network RPC unless the consumer adds auth.

## Invariants
1. Endpoint types implement `IEndpoint`; public base is `Endpoint<TRequest[, TResponse]>`.
2. AOT: do **not** rely on reflection discovery; use `AddFastEndpoints(DiscoveredTypes.All)` (+ generator).
3. Mappers/validators discovered types are typically treated as singletons for performance; no per-request state in mappers.

## Sources
- `Src/Library/Main/MainExtensions.cs`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Src/Library/Endpoint/Endpoint.cs`
- `Src/**/*.csproj`
- `Src/Library/Metadata.cs`
