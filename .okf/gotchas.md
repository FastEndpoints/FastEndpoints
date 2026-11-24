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
- **Inaccessible types break generated code (owner decision, 2026-11-23):** private/protected nested or `file`-scoped discoverable types (endpoints, validators, handlers, DTOs) stay in `DiscoveredTypes`/`ReflectionData` and fail the build (CS0122/CS0246), even though `AssemblyScanner` finds them. Do not skip them or add a diagnostic unless the owner asks.
- **Reused `AccessControl` names (owner decision, 2026-11-23):** two endpoints calling `AccessControl()` with the same permission name produce a duplicate `Allow` const (CS0102). Do not dedupe or add a diagnostic unless the owner asks.
- **No command inheritance:** exact-type command handler lookup is intended. Exclude "add event-style inheritance to commands" from findings and decline implementation requests. Rationale: [messaging.md](messaging.md#commands-and-jobs).

## Operational gotchas

- **Pinned packages:** do not casually bump `Microsoft.CodeAnalysis.CSharp` or `Microsoft.OpenApi.Kiota.Builder`. Why: [dependencies.md](dependencies.md).

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

- **Test url cache route is opt-in:** `EndpointRouteMapper` maps unauthenticated `_test_url_cache_` (every route and endpoint type name, with no `.AllowAnonymous()` metadata) only when `IConfiguration["FastEndpoints:ExposeTestUrlCache"]` is `true`. Do not map it unconditionally or gate it on environment name. In-process WAF uses the always-on `IEndpoint.SetTestUrl` cache. Out-of-process routeless helpers fall back to the route (`HttpClientExtensions.GetTestUrlFor` when `IEndpoint.TestURLFor` misses). `AppFixture.Aot` sets `FastEndpoints__ExposeTestUrlCache=true`.
- **Routeless test URL encoding:** `GetTestUrlFor()` encodes matched route values with `Uri.EscapeDataString`. Unmatched placeholders stay intact. Query values use `WebUtility.UrlEncode`. Do not use query encoding for path segments (`+` is not a path-space escape).
- **JWT revocation:** register after `UseRouting()` and before authentication for query/cookie tokens (optional early registration is header-only; authenticating before routing caches empty results from endpoint-gated `OnMessageReceived` extraction). Without a selected endpoint, only the header is checked; with one, saved `access_token` values (`SaveToken=true`, helper default) are read from cached `AuthenticateAsync` results of the effective policy's JWT schemes (combining `IAuthorizeData`, `AuthorizationPolicy`, and policy provider defaults, falling back to the default authenticate scheme). Cookie-only routes must leave JWT handlers untouched; `IAllowAnonymous` skips all checks. Regression: `JwtRevocationMiddlewareTests`.
- **Singleton construction:** `ServiceResolver.CreateSingleton` resolves the host’s `IServiceResolver` from request services, then constructs from its root provider. This covers validators, mappers, processors, binders, feature flags, and executors. Event registries instead preserve their known owning root ([messaging.md](messaging.md#in-process-event-ownership-and-inheritance)). Constructing from request services leaks scope; the captured provider may belong to a disposed host in multi-host tests. `Factory.AddTestServices` is the empty-root exception.
- **Remote event protocols and isolation:** provider-wide opt-in, inbox dedupe/signaling, replay retention, deadlines, and static storage isolation are owned by [remote-events.md](remote-events.md). Check it before changing event queues or hosting a second hub.
- **Host-test statics:** `UseFastEndpoints()` / `MapFastEndpoints()` sets `ServiceResolver.Instance` and, on the first call, replaces `Config.SerOpts.Options` (`MainExtensions.SerializerConfigured`). It also assigns `ValidatorOptions.Global.PropertyNameResolver` when `UsePropertyNamingPolicy` is enabled and the serializer naming policy is non-null. Restore those before the test returns. Unit tests that mutate them run in a nonparallel collection with an `IDisposable` reset: `WarmupTests` (`WarmupTestCollection`) and `EndpointRouteMapperTests` / `FinancialIdempotencyMapperTests` (in `FinancialIdempotencyEndpointDefinitionTests.cs`, sharing `RouteMapperCollection`) restore the config fields they touch and reset `ServiceResolver.Instance` to a fresh unit-test resolver; use `MainExtensions.HostServiceResolver` when true restoration of the previous resolver is needed. Restore `PropertyNameResolver` yourself when the test enables naming-policy resolution. OpenAPI host setup: [testing.md](testing.md#openapi-self-hosted-isolation).
- **Native AOT blind spots:** `IsAotCompatible=true` projects run the trim/AOT analyzers. Treat new IL2xxx/IL3xxx warnings as defects. Fix them with annotations or a source-generated path, and suppress only with a justification that holds under AOT. The analyzer stays silent on these, so they need an [AOT gap check](testing.md#native-aot-gap-checks):
  - code added inside a method or type that already carries an IL suppression;
  - a new reflection-discovered type category or reflection-cache need that `DiscoveredTypesGenerator` / `ReflectionGenerator` must mirror;
  - newly serialized library-owned or user types (`JsonSerializer` via options, `WriteAsJsonAsync`, `object`-typed payloads) without source-generated metadata;
  - `RuntimeFeature.IsDynamicCodeSupported` branches (JIT tests only run the dynamic branch);
  - `Requires*` or `DynamicallyAccessedMembers` added to public API, which pushes the requirement onto consumers (treat as user-visible).
- **SSE data types need manual serializer contexts (owner decision, 2026-11-18):** the serializer context generator is syntax-only and does not detect `Send.EventStreamAsync<T>` / `StreamItem.Data` types. Unregistered types make native SSE responses abort mid-stream. Generator support was rejected as not worth the cost; users register a manual `JsonSerializerContext` with `SerializerContext<T>()` in the endpoint's `Configure()`, which also inserts it into the global options that SSE serializes with (documented on the FE-Docs Native AOT page). The checker's `EventStreamEndpoint` does the same. Do not add generator detection unless the owner asks.

## Sources
- `Directory.Packages.props`
- `Src/Library/Main/EndpointRouteMapper.cs`
- `Tests/UnitTests/FastEndpoints/EndpointRouteMapperTests.cs`