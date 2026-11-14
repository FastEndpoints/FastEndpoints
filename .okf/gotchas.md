---
type: Reference
title: Gotchas
description: Non-obvious traps for agents working in the FastEndpoints monorepo.
tags: [gotcha]
---

# Gotchas

## Accepted policies and review exclusions

- **Detached broadcast error callbacks are outside the shutdown drain (owner decision, 2026-11-10):** `OnSerializeEventError` and `OnInMemoryQueueOverflow` remain fire-and-forget. They may outlive host shutdown and encounter disposed dependencies. This is accepted behavior, not a shutdown-drain bug or improvement finding. Preserve it unless the owner requests a contract change.
- **Poison-event disposal (owner decision, 2026-10-03):** ordinary and ACK dispatchers complete/discard unreadable events after three deserialization failures, even if `OnDeserializeEventError<TEvent>` is absent or throws. Callback failures are logged; completed rows become purgeable. Recovery/DLQ persistence is consumer-owned and best-effort. Cancellation leaves durable rows pending. Exclude completion-after-failed-recovery from bug/improvement findings unless the owner requests a contract change. Sources: `HubContext.DeserializeEvent`, `EventHubExceptionReceiver.OnDeserializeEventError`, `EventQueueTests.Deserialization.cs`.
- **Slow-successful ACK inbox insertion (owner decision, 2026-10-02):** refresh `ExpireOn` before each `StoreEventAsync` attempt, not after success. A successful call longer than that window may persist an expired row, acknowledge hub completion, and execute no handlers. Exclude this boundary from bug/improvement findings unless the owner requests a contract change. Duplicates preserve persisted expiry/completion/retention, including lost commit responses; ordinary subscriber timing is unchanged. Coverage: `EventQueueTests.AckStoreExpiry.cs`.

## Operational gotchas

- **AOT discovery:** use `AddFastEndpoints(DiscoveredTypes.All)` plus the Generator analyzer, not reflection discovery. Generation constraints and output editability: [generated-code.md](generated-code.md).
- **Pinned packages:** do not casually bump `Microsoft.CodeAnalysis.CSharp` or `Microsoft.OpenApi.Kiota.Builder`. Why: [dependencies.md](dependencies.md).
- **User DotSettings:** `*.sln.DotSettings.user` is personal. Not repo policy.

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

- **Test url cache route is opt-in:** `EndpointRouteMapper` maps anonymous `_test_url_cache_` (every route and endpoint type name) only when `IConfiguration["FastEndpoints:ExposeTestUrlCache"]` is `true`. Do not map it unconditionally or gate it on environment name. In-process WAF uses the always-on `IEndpoint.SetTestUrl` cache. Out-of-process routeless helpers fall back to the route (`HttpClientExtensions.GetTestUrlFor` when `IEndpoint.TestURLFor` misses). `AppFixture.Aot` sets `FastEndpoints__ExposeTestUrlCache=true`.
- **Routeless test URL encoding:** `GetTestUrlFor()` encodes matched route values with `Uri.EscapeDataString`. Unmatched placeholders stay intact. Query values use `WebUtility.UrlEncode`. Do not use query encoding for path segments (`+` is not a path-space escape).
- **JWT revocation:** check the header on requests without a selected endpoint, and check saved `access_token` from selected JWT schemes’ cached `AuthenticateAsync` results only when an endpoint is selected. Combine endpoint `IAuthorizeData` and direct `AuthorizationPolicy` metadata with the policy provider (including default/fallback policies); use the default authenticate scheme when the effective policy names no schemes. Cookie-only routes must leave unrelated JWT handlers untouched. Register after `UseRouting()` and before authentication for query/cookie tokens; an optional early registration is header-only. Early authentication caches empty results from endpoint-gated `OnMessageReceived` extraction. Saved tokens require `SaveToken=true` (helper default). `IAllowAnonymous` skips all checks. `JwtRevocationMiddlewareTests` covers endpoint-gated query/cookie extraction across early and routed checks, valid/revoked tokens, downstream authentication, and one validation per request.
- **Singleton construction:** `ServiceResolver.CreateSingleton` resolves the host’s `IServiceResolver` from request services, then constructs from its root provider. This covers validators, mappers, processors, binders, feature flags, and executors. Event registries instead preserve their known owning root ([messaging.md](messaging.md#in-process-event-ownership-and-inheritance)). Constructing from request services leaks scope; the captured provider may belong to a disposed host in multi-host tests. `Factory.AddTestServices` is the empty-root exception.
- **Remote event protocols and isolation:** provider-wide opt-in, inbox dedupe/signaling, replay retention, deadlines, and static storage isolation are owned by [remote-events.md](remote-events.md). Check it before changing event queues or hosting a second hub.
- **Host-test statics:** `UseFastEndpoints()` / `MapFastEndpoints()` sets `ServiceResolver.Instance` and, on the first call, replaces `Config.SerOpts.Options` (`MainExtensions.SerializerConfigured`). It also assigns `ValidatorOptions.Global.PropertyNameResolver` when `UsePropertyNamingPolicy` is enabled and the serializer naming policy is non-null. Restore those before the test returns. Unit tests that mutate them join `[Collection(RouteMapperCollection.Name)]` and follow the `IDisposable` reset in `EndpointRouteMapperTests` (also `RoutePrefixTests`, `WarmupTests`, `FinancialIdempotencyMapperTests`). OpenAPI host setup: [testing.md](testing.md#openapi-self-hosted-isolation).

## Sources
- `Directory.Packages.props`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Tests/UnitTests/FastEndpoints/EndpointRouteMapperTests.cs`
