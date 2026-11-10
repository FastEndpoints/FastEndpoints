---
type: Reference
title: Gotchas
description: Non-obvious traps for agents working in the FastEndpoints monorepo.
tags: [gotcha]
---

# Gotchas

## Accepted policies and review exclusions

- **Detached broadcast error callbacks are outside the shutdown drain (owner decision, 2026-11-10):** `OnSerializeEventError` and `OnInMemoryQueueOverflow` remain fire-and-forget. They may outlive host shutdown and encounter disposed dependencies. This is accepted behavior, not a shutdown-drain bug or improvement finding. Preserve it unless the owner requests a contract change.
- **Poison-event disposal is an accepted reliability policy (owner decision, 2026-10-03):** ordinary and ACK hub dispatchers complete/discard an unreadable event after three deserialization failures, including when `OnDeserializeEventError<TEvent>` is absent or throws (for example, a DLQ write fails). A throwing callback is logged; the completed source row becomes eligible for normal purging. Recovery/DLQ persistence is consumer-owned and best-effort. Future bug hunts should exclude this completion-after-failed-recovery behavior as a bug or improvement finding. Preserve this policy unless the owner explicitly requests a contract change. Cancellation still leaves durable rows pending. Sources: `HubContext.DeserializeEvent`, `EventHubExceptionReceiver.OnDeserializeEventError`, and `EventQueueTests.Deserialization.cs` (`event_deserialization_terminal_failure_notifies_before_completion_and_continues`, including throwing-receiver variants).
- **ACK inbox execution expiry starts at each insertion attempt:** the receiver refreshes `ExpireOn` immediately before each `StoreEventAsync` call. Failed attempts leave the next attempt a fresh execution window. A single successful call exceeding that window can still persist an expired row and be acknowledged. **Accepted review exclusion (owner decision, 2026-10-02):** future bug hunts should skip this slow-successful-insert expiry boundary as a bug or improvement finding. The accepted outcome is hub completion with an expired inbox row and zero handler executions. Preserve attempt-time expiry unless the owner explicitly requests a contract change. Coverage: `EventQueueTests.AckStoreExpiry.cs`, `delivery_ack_successful_store_longer_than_execution_window_remains_expired`. Duplicate detection preserves persisted expiry, completion, and retention, including a commit whose response was lost. Ordinary subscribers keep their existing expiry timing.

## Operational gotchas

- **Generation contracts:** analyzer packaging, open-generic discovery exclusions, reflection namespaces, access-control constants, serializer CLI setup, and generated outputs: [generated-code.md](generated-code.md).
- **AOT discovery:** reflection `AddFastEndpoints()` is not supported under AOT. Use `AddFastEndpoints(DiscoveredTypes.All)` plus the Generator analyzer.
- **Pinned packages:** do not casually bump `Microsoft.CodeAnalysis.CSharp` or `Microsoft.OpenApi.Kiota.Builder`. Why: [dependencies.md](dependencies.md).
- **NuGet duplicate versions:** use `--skip-duplicate` when pushing solution packages. Independently versioned Agents may already be published. Release commands: [workflows.md](workflows.md#pack-and-publish).
- **User DotSettings:** `*.sln.DotSettings.user` is personal. Not repo policy.

### Agents compatibility

MCP behavior, independent versioning, and the Library friend-assembly binary contract: [agents.md](agents.md). Read before changing this surface.

### HTTP and binding

- **OpenAPI changes:** read [openapi.md](openapi.md) for generation ownership, schema isolation, validation, examples, tags, and compatibility. Regression map: [testing.md](testing.md#openapi-regression-map).
- **Enum binding:** default `AllowUndefinedEnumValues=false` rejects undefined numerics and comma/repeated-name lists for non-flags enums (`Enum.TryParse` ORs names into potentially valid members). Defined `[Flags]` combinations still bind; `true` restores raw `TryParse`. JSON bodies follow STJ options.
- **Validation hot path:** keep `Endpoint.HandleValidationFailure` private and separate from `ExecAsync`. An async local introduces per-request closure allocation. `ValidationFailed` is already a public property.
- **DataAnnotations service scope:** pass the endpoint's `HttpContext.RequestServices` explicitly through `ValidateRequest` and recursive validation. Each object's `ValidationContext` must use that provider, not the global resolver or an ambient context, so custom attributes share the endpoint's scoped services. Keep the recursive helper static to avoid captured state.
- **`ResponseCacheExecutor` caches `Cache-Control` once per endpoint.** That is valid only because `ResponseCache(...)` is `ThrowIfLocked()`. A runtime mutation of location or duration must invalidate the cached string.
- **Multi-route mapping:** `EndpointRouteMapper` clears `AttribsToForward` and locks the definition after the route loop. Moving either inside loses attribute metadata or locks on the first route. `EndpointExtensions.Initialize` populates forwarded attributes only for attribute-configured endpoints.
- **`DontVersion()` vs `Version(0)`:** `Version(0)` is still unset. `EpVersion.Init()` then applies `DefaultVersion`. `DontVersion()` keeps version 0 and omits the route segment. Do not use `-1` or any negative sentinel. OpenAPI release-group filtering is `version >= MinEndpointVersion && version <= MaxEndpointVersion`, and `Current` is public metadata.
- **Handler short-circuit is opt-in.** Pre-processors skip the handler when `ResponseStarted` is true. `OnBeforeHandle*` does not, unless `DontExecuteHandlerIfResponseStarted()` set `SkipHandlerIfResponseStarted`. That early return also skips `OnAfterHandle*`. Post-processors still run.

### Testing and service lifetimes

- **WAF cache:** one cached factory per `AppFixture` type. Static state on that factory leaks across tests. Register doubles in `ConfigureServices`. Teardown hook: [testing.md](testing.md).
- **Test url cache route is opt-in:** `EndpointRouteMapper` maps anonymous `_test_url_cache_` (every route and endpoint type name) only when `IConfiguration["FastEndpoints:ExposeTestUrlCache"]` is `true`. Do not map it unconditionally or gate it on environment name. In-process WAF uses the always-on `IEndpoint.SetTestUrl` cache. Out-of-process routeless helpers fall back to the route (`HttpClientExtensions.GetTestUrlFor` when `IEndpoint.TestURLFor` misses). `AppFixture.Aot` sets `FastEndpoints__ExposeTestUrlCache=true`.
- **Routeless test URL encoding:** `GetTestUrlFor()` encodes matched route values with `Uri.EscapeDataString`. Unmatched placeholders stay intact. Query values use `WebUtility.UrlEncode`. Do not use query encoding for path segments (`+` is not a path-space escape).
- **JWT revocation:** check the header on requests without a selected endpoint, and check saved `access_token` from selected JWT schemes’ cached `AuthenticateAsync` results only when an endpoint is selected. Combine endpoint `IAuthorizeData` and direct `AuthorizationPolicy` metadata with the policy provider (including default/fallback policies); use the default authenticate scheme when the effective policy names no schemes. Cookie-only routes must leave unrelated JWT handlers untouched. Register after `UseRouting()` and before authentication for query/cookie tokens; an optional early registration is header-only. Early authentication caches empty results from endpoint-gated `OnMessageReceived` extraction. Saved tokens require `SaveToken=true` (helper default). `IAllowAnonymous` skips all checks. `JwtRevocationMiddlewareTests` covers endpoint-gated query/cookie extraction across early and routed checks, valid/revoked tokens, downstream authentication, and one validation per request.
- **Singleton construction:** `ServiceResolver.CreateSingleton` resolves the host’s `IServiceResolver` from request services, then constructs from its root provider. This covers validators, mappers, processors, binders, feature flags, event handlers, and executors. Constructing from request services leaks scope; the captured provider may belong to a disposed host in multi-host tests. `Factory.AddTestServices` is the empty-root exception.
- **Remote event protocols and isolation:** provider-wide opt-in, inbox dedupe/signaling, replay retention, deadlines, and static storage isolation are owned by [remote-events.md](remote-events.md). Check it before changing event queues or hosting a second hub.
- **Host-test statics:** `UseFastEndpoints()` / `MapFastEndpoints()` sets `ServiceResolver.Instance` and, on the first call, replaces `Config.SerOpts.Options` (`MainExtensions.SerializerConfigured`). It also assigns `ValidatorOptions.Global.PropertyNameResolver` on every call. Restore those before the test returns. Unit tests that mutate them join `[Collection(RouteMapperCollection.Name)]` and follow the `IDisposable` reset in `EndpointRouteMapperTests` (also `RoutePrefixTests`, `WarmupTests`, `FinancialIdempotencyMapperTests`). OpenAPI host setup: [testing.md](testing.md#openapi-self-hosted-isolation).
- **Test receivers are append-only spies.** Match rules and capture timing: [testing.md](testing.md).

### Remote RPC and reflection

Wire-format selection and protobuf compatibility: [messaging.md](messaging.md#remote-rpc-and-reflection). Read before changing this surface.

### Financial idempotency

Reservation settlement, response capture, and persistent identity contracts: [financial-idempotency.md](financial-idempotency.md). Read before changing this surface.

### Messaging and jobs

Command dispatch lifetimes, job deduplication, hot paths, and Native AOT constraints: [messaging.md](messaging.md#messaging-and-jobs). Read before changing this surface.

## Sources
- `Directory.Packages.props`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Tests/UnitTests/FastEndpoints/EndpointRouteMapperTests.cs`
