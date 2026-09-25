---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Financial-mode HTTP idempotency with <code>FinancialIdempotency()</code></summary>

Payment-style POST/PUT endpoints can reserve an idempotency key before the handler runs, replay the original 2xx, and `409` when the same key is reused with a different payload. This is a dedicated store and middleware, not an output-cache mode. Fingerprint `Idempotency()` is unchanged.

Register `AddFinancialIdempotency()` (in-memory store by default, or `AddFinancialIdempotency<TStore>()` for Redis/SQL) and `UseFinancialIdempotency()` after routing/auth, next to `UseOutputCache()`. Do not call both `Idempotency()` and `FinancialIdempotency()` on the same endpoint. Distributed stores must implement atomic `TryBegin`; in-flight keys return `409` so clients retry. Fail-closed `500` if a 2xx cannot be replayed. Requires an explicit trusted stable `CallerScope`. Ownership-token settlement prevents stale writers; active and uncertain reservations never auto-expire. Concurrent requests return immediate `409`. Exceptions and ambiguous non-2xx responses retain protection; only explicit no-side-effects rejection releases a key. Bounded lifecycle capture includes empty responses and pending pipe bytes. Uploaded file contents participate in the hash. Breaking store/identity changes require coordinated migration. A durable application store and transactional/downstream idempotency remain deployment obligations.

```csharp
bld.Services.AddFastEndpoints().AddFinancialIdempotency(c =>
    c.CallerScope = ctx => ctx.User.FindFirst("account_id")?.Value);
app.UseFinancialIdempotency().UseFastEndpoints();

sealed class Charge : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("charges");
        FinancialIdempotency(o => o.Duration = TimeSpan.FromHours(24));
    }
}
```

</details>

<details><summary>Exclude an endpoint from route versioning with <code>DontVersion()</code></summary>

When `Versioning.DefaultVersion` is set, every endpoint that does not call `Version(n)` gets that version on its route. Call `DontVersion()` to keep an endpoint at version 0 so no version segment is added (`/health` instead of `/v1/health`).

`Version(0)` is still treated as unset and receives the default. Last call wins: `DontVersion()` then `Version(1)` versions the endpoint; `Version(1)` then `DontVersion()` unversions it.

```csharp
public override void Configure()
{
    Get("health");
    AllowAnonymous();
    DontVersion();
}
```

</details>

## Fixes 🪲

<details><summary>Comma-separated enum names no longer bind as a different member</summary>

Query, route, form, header, cookie, and claim binding treated <code>Monday,Tuesday</code> as <code>Wednesday</code> when that bitwise OR was itself a defined enum member. Repeated values such as <code>?day=Monday&amp;day=Tuesday</code> did the same. With <code>AllowUndefinedEnumValues</code> left at its default <code>false</code>, enums that are not <code>[Flags]</code> now reject those inputs. <code>[Flags]</code> enums still accept a comma list when the combined value is a defined member. Set <code>Binding.AllowUndefinedEnumValues = true</code> to keep the old <code>Enum.TryParse</code> behavior. JSON body enums are unchanged and still follow your serializer options.

</details>

<details><summary>Validators nested inside an open generic no longer break <code>DiscoveredTypes</code> generation</summary>

A discovered type nested inside an open generic, such as <code>BaseValidator&lt;T&gt;.ChildValidator</code>, was emitted as <code>Preserve&lt;BaseValidator&lt;T&gt;.ChildValidator&gt;()</code>. That does not compile, and making the nested type private failed generation for the same reason. Those types are now skipped, which is what reflection discovery already does. Move the nested type out of the open generic if it should be registered.

</details>

<details><summary>No-result command middleware runs under Native AOT</summary>

After `Void` became a struct, Native AOT could not resolve `IEnumerable<ICommandMiddleware<TCommand, Void>>`, so middleware registered for no-result commands was skipped. Those pipelines now use the types recorded at `AddCommandMiddleware` time. Closed `Register<MyCommand, Void, MyMiddleware>()` works without consumer code changes. Open-generic `Register(typeof(Foo<,>))` is still unsupported for void commands under Native AOT and now throws instead of silently skipping.

</details>

<details><summary>Void-result job queues and command execution work under Native AOT again</summary>

After `Void` became a struct, Native AOT apps that call `UseJobQueues()` crashed at startup because MS.DI cannot close open generics over a valuetype. Job queues for commands that return no result are now constructed directly, and closed over an internal reference type rather than `Void`. The same valuetype limitation also blocked `ICommand.ExecuteAsync()` / command-rules `ExecuteNow` (missing native code for `CommandHandlerExecutor<TCommand, Void>`). Those void commands now use a class-only executor. No consumer code changes are required.

</details>

<details><summary>Serializer context generation now fully qualifies enum type arguments</summary>

Auto-generated `[JsonSerializable]` attributes left enums as bare names when they appeared as generic arguments, such as `Dictionary<MyEnum, MyDto[]>`. The generated context then failed to compile (`CS0246`) and STJ source-gen skipped metadata for the enum (`SYSLIB1030`). Enums are now indexed like other types and emitted with their full namespace.

</details>

<details><summary>Overlapping OpenAPI document requests no longer throw or return a truncated spec</summary>

`MapOpenApi()` rebuilds the document on every request and does not serialize generation. Visual Studio and Scalar often hit `/openapi/*.json` at the same time on startup, which made FluentValidation schema mapping and `oneOf` cleanup mutate the same schema objects. That produced `IndexOutOfRangeException` / `Collection was modified` failures, or a document with only some of the paths. Each generation now keeps its own mutation state, so overlapping fetches complete with a full document.

</details>

<details><summary>Singleton validators, mappers, processors and event handlers no longer capture scoped services from the first request</summary>

Validators, mappers, pre/post-processors, event handlers and other types that FastEndpoints caches as singletons were built from the DI scope of whichever request first needed them, unless `Warmup()` was enabled. Scoped constructor dependencies (such as a `DbContext` or a current user service) were therefore captured from that first request and reused by every later request, without triggering DI scope validation. These singletons are now always built from the root service provider, the same as with `Warmup()`. Injecting a scoped service into their constructors now throws when scope validation is enabled (the default in the Development environment), as the docs describe. Resolve scoped services per request with `Resolve<T>()` or a new scope instead.

</details>

<details><summary>JWT revocation middleware now matches the <code>Bearer</code> scheme case-insensitively</summary>

`JwtRevocationMiddleware` only checked tokens sent with an exact `Bearer ` prefix, while the JWT bearer authentication handler accepts the scheme in any casing. A revoked token sent as `Authorization: bearer <jwt>` therefore skipped `JwtTokenIsValidAsync()` but still authenticated. The prefix is now matched case-insensitively, so every token the authentication handler reads from the `Authorization` header goes through the revocation check.

</details>

<details><summary>JWT revocation middleware now checks tokens from any source and before routing</summary>

`JwtRevocationMiddleware` only checked the token in the `Authorization` header. Apps that also accept access tokens from the query string or a cookie via `JwtBearerEvents.OnMessageReceived` (common for SSE and SignalR clients) authenticated revoked tokens sent that way without calling `JwtTokenIsValidAsync()`. The middleware now also checks the token that each JWT bearer authentication scheme actually accepted. Authentication results are cached per request, so tokens are not validated twice.

This relies on `JwtBearerOptions.SaveToken`, which `AddAuthenticationJwtBearer()` now enables by default. If you register JWT bearer auth with `AddJwtBearer()` directly and accept tokens from somewhere other than the `Authorization` header, set `SaveToken = true`.

The middleware also skipped every request when no endpoint had been matched yet, for example when `UseJwtRevocation<T>()` was registered before an explicit `UseRouting()` call. Such requests are now checked, and only endpoints that allow anonymous access are skipped.

</details>

<details><summary>Routeless test helpers now URL-encode route parameter values</summary>

Route parameter values generated by `GetTestUrlFor()` are now URL-encoded, preventing reserved characters such as `#`, `?`, and spaces from truncating or altering the request URL.

</details>

<details><summary>Nullable collection properties no longer get <code>const: null</code> in OpenAPI documents</summary>

`FastEndpoints.OpenApi` 8.3.0 emitted nullable collection properties with a sibling `"const": null`. JSON Schema applies keywords together, so only `null` validated; a populated array failed against the schema describing it.

The property now serializes as a nullable array only:

```json
"children": {
    "type": [
        "null",
        "array"
    ],
    "items": {
        "$ref": "#/components/schemas/Child"
    }
}
```

Visible with `Microsoft.OpenApi` 2.11.0 or later.

</details>

## Improvements 🚀

<details><summary>Warmup no longer precompiles the data-annotations validation graph when it's disabled</summary>

`Warmup()` unconditionally walked and precompiled each request DTO's data-annotations validation graph (bindable props + getters), even though that graph is only ever used when `Validation.EnableDataAnnotationsSupport` is turned on. That startup-only work is now skipped when the setting is left at its default (off), which is the common case.
</details>

<details><summary>SSE <code>StreamItem.Id</code> is now settable after construction</summary>

`StreamItem.Id` was `init`-only, so SSE endpoints that own an incrementing event-id sequence had to pass a counter into helper methods or clone each item just to stamp the id. `Id` can now be assigned after construction:

```csharp
var item = SomeHelper();
item.Id = (i++).ToString();
```

`EventName`, `Data`, and `Retry` remain `init`-only.

</details>

<details><summary>FluentValidation rules now apply to OpenAPI query, path, header, and cookie parameters</summary>

`FastEndpoints.OpenApi` previously applied validator constraints only to request body schemas. Bodyless GET/HEAD endpoints (and mixed POST properties marked `[QueryParam]` / `[FromHeader]` / `[FromCookie]`) therefore omitted `required`, `minLength`, patterns, and numeric ranges from the generated document.

Those rules now apply to DTO-bound operation parameters as well, using the same conditional-rule behavior as request bodies. Client generators that derive types from parameters will now see the constraints.

</details>

<details><summary>Route mapping no longer rebuilds authorization metadata once per HTTP verb</summary>

Endpoints with multiple HTTP verbs and/or routes had their `AuthorizeAttribute[]` rebuilt from scratch for every verb of every route, even though the result depends only on endpoint-level settings (roles, policies, schemes) and never varies by verb or route. That metadata is now built once per endpoint definition and reused for every verb/route it's registered under, skipping the work entirely when every verb is anonymous.
</details>
  
<details><summary>Command execution no longer builds a handler-interface <code>Type</code> it doesn't need on the hot path</summary>
`ExecuteAsync` computed a closed generic handler interface type via `MakeGenericType` on every command and stream-command dispatch, but that type is only read the first time a generic command type is seen, or when a unit test has registered a fake handler. Both call sites now compute it lazily, only when one of those two conditions is actually true, removing an unnecessary reflection call from the common case of executing a registered, non-generic command outside of a test.
</details>

<details><summary>Required-property validation no longer rebuilds a hash set per request</summary>
`BinderContext.UnboundRequiredProperties` used `Enumerable.Except` to diff the endpoint's required property names against the ones actually bound, which internally builds a fresh `HashSet<string>` from the bound-properties list on every request that declares required properties. The bound-properties collection is now itself a `HashSet<string>` populated as binding happens, so the diff is a plain lookup per required property instead of a rebuild-then-diff. Behavior and the case-insensitive comparison are unchanged.
</details>

## Minor Breaking Changes ⚠️

<details><summary><code>Group.Configure()</code> is no longer overridable</summary>

<code>Configure()</code> is now non-virtual, so calling it from a group constructor no longer raises a "virtual member call in constructor" warning. The route prefix is still applied, the group's own action still runs, and <code>SubGroup&lt;TParent&gt;</code> still runs the parent group after that.

If a <code>Group</code> subclass overrode <code>Configure()</code>, remove the override and call <code>Configure()</code> from the constructor.
</details>

<details><summary>Test url cache route is now opt-in</summary>

The internal `_test_url_cache_` route used by routeless test helpers (`GETAsync<TEndpoint>()` etc.) when testing an out-of-process app is no longer mapped by default, since it exposed every endpoint route and type name to anonymous callers. Apps now only map it when the configuration value `FastEndpoints:ExposeTestUrlCache` is `true`.

In-process `AppFixture` (WAF) tests are unaffected, and Native AOT `AppFixture` tests set it automatically. Aspire `DistributedApplication` tests must set it on the tested project resource:

```csharp
var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireApp_AppHost>(ct);
appHost.CreateResourceBuilder<ProjectResource>("apiservice")
       .WithEnvironment("FastEndpoints__ExposeTestUrlCache", "true");
```

Never enable it in production.

</details>

<details><summary><code>ProblemDetails.Errors</code> is now <code>IReadOnlyCollection&lt;Error&gt;</code> instead of <code>IEnumerable&lt;Error&gt;</code></summary>

`Errors` was a lazy `IEnumerable<Error>`. When `AllowDuplicateErrors` was enabled, that sequence got re-enumerated up to three times per error response (once when reading `Errors.Count()`/`Errors.First()` to build `Detail`, again during JSON serialization), rerunning `PropertyNamingPolicy.ConvertName` for every `Error` each time. `Errors` is now always backed by a concrete collection (a materialized array when duplicates are allowed, the existing deduplicating `HashSet` otherwise), so it is only ever enumerated once.

Reading `Errors` is unaffected. Code that assigns `Errors` directly to a lazy `IEnumerable<Error>` (for example a custom `ResponseBuilder` that constructs its own `ProblemDetails`) needs to materialize it first, since the setter no longer accepts a plain `IEnumerable<Error>`.
</details>

<details><summary><code>Void</code> is now a struct instead of a class</summary>

`Void` (behind `ICommand` and `Task<Void>` send methods) is now a `readonly struct`. Synchronously completing no-result sends and command dispatches no longer allocate a `Task`.

This breaks `where TResult : class` over `ICommand<TResult>` (or `IServerStreamCommand<TResult>`) when `TResult` is `Void`. Drop the constraint, or add a sibling API constrained on `ICommand`.

</details>