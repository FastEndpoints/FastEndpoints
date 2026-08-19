---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>'FastEndpoints.CommandRules' package for rule-based command dispatch</summary>

A new `FastEndpoints.CommandRules` package is now available for turning arbitrary input into one or more commands using small, ordered rules.

It's useful when an application event, webhook payload, request DTO, or domain object needs to fan out into different command-bus actions without putting branching logic in endpoints or handlers. Rules evaluate the input, build a command plan, and the dispatcher executes the selected commands immediately or queues them as jobs.

```csharp
// map input model to a rule
bld.Services.AddCommandRules(o => o.Register<OrderPlaced, OrderPlacedRule>());

// define the rule to specify which commands should execute
sealed class OrderPlacedRule : CommandRule<OrderPlaced>
{
    public override bool CanHandle(OrderPlaced input)
        => input.IsPaid;

    public override IEnumerable<PlannedCommand> Build(OrderPlaced input)
    {
        yield return PlannedCommand.Create(new ReserveStock(input.OrderId));

        if (input.SendReceipt)
        {
            yield return new PlannedCommand(new SendReceipt(input.OrderId))
            {
                Mode = CommandDispatchMode.QueueAsJob
            };
        }
    }
}

// inject ICommandDispatcher<OrderPlaced> where the event/input is handled
await dispatcher.DispatchAsync(orderPlaced, ct);
```

</details>

<details><summary>Cached 'AppFixture' WAF disposal hook</summary>

`AppFixture<TProgram>` can now run final teardown once when its cached `WebApplicationFactory<TProgram>` is disposed at the end of a test assembly.

Override `OnCachedWafDisposedAsync()` in your fixture to clean up resources tied to the shared WAF instance. The hook runs after all fixture users are done, and requires cached WAF mode with `[assembly: EnableAdvancedTesting]`.

```csharp
sealed class App : AppFixture<Program>
{
    protected override async ValueTask OnCachedWafDisposedAsync()
    {
        await ResetExternalResourceAsync();
    }
}
```

</details>

<details><summary>Idempotency support for job queues</summary>

Job queues can now enforce business-key idempotency so the same logical action is only stored once. Configure a key selector per command type. When a non-empty key collides on the same queue, the duplicate is discarded with a warning log and `QueueJobAsync` returns the existing tracking id.

Requires the storage record to implement `IHasIdempotencyKey` and the storage provider to enforce uniqueness on `QueueID + IdempotencyKey` while the row exists (including completed jobs). On unique violation, throw `DuplicateJobException` with the existing tracking id. The library does not unwrap raw storage errors.

```csharp
// enable idempotency for selected command types
app.UseJobQueues(o =>
{
    o.IdempotencyKeyFor<ProcessOrderCommand>(c => c.OrderId);
    o.IdempotencyKeyFor<ChargeCustomerCommand>(c => c.PaymentId.ToString("D"));
});

// storage record must implement IHasIdempotencyKey
sealed class JobRecord : IJobStorageRecord, IHasIdempotencyKey
{
    public string? IdempotencyKey { get; set; }
    ...
}

// provider throws DuplicateJobException on unique violation
public async Task StoreJobAsync(JobRecord job, CancellationToken ct)
{
    try
    {
        await db.SaveAsync(job, ct);
    }
    catch (/* unique index violation */)
    {
        var existing = await db.FindByQueueAndKeyAsync(job.QueueID, job.IdempotencyKey!, ct);
        throw new DuplicateJobException(existing.TrackingID, job.IdempotencyKey, job.QueueID);
    }
}

// first call stores the job; retries return the same tracking id
var trackingId = await new ProcessOrderCommand { OrderId = "ORD-42" }.QueueJobAsync();
```

Null/empty/whitespace keys from the selector are treated as no key and are not deduped. Uniqueness lasts until the row is purged. After delete, the same key may be reused.

</details>

<details><summary>gRPC reflection support for remote command handlers</summary>

Remote command handlers can now be discovered and described via standard gRPC server reflection, so grpcurl and Postman work against a handler server without a hand-authored `.proto`, and any protoc/buf toolchain can generate clients for non-dotnet consumers. It lives in the new opt-in `FastEndpoints.Messaging.Remote.Reflection` package.

Reflection describes a protobuf schema, so the server has to be speaking protobuf rather than the default MessagePack. The wire format is now pluggable via `IRpcMarshallerFactory`, and `ProtobufMarshallerFactory` is included. Command types need no protobuf attributes, and their public properties are mapped alphabetically and numbered from 1. The descriptors are generated from the very same model that serializes to the wire, so the published schema can't drift from the bytes.

```csharp
// server: opt in to the protobuf wire format + reflection
bld.AddHandlerServer(marshaller: new ProtobufMarshallerFactory());
bld.Services.AddHandlerReflection();

app.MapHandlers(h => h.Register<MyCommand, MyCommandHandler, MyResult>());
app.MapHandlerReflection(); // returns a builder, so .RequireAuthorization() can be chained

// client: the matching wire format, set before registering anything
app.MapRemote("http://localhost:6000", c =>
{
    c.MarshallerFactory = new ProtobufMarshallerFactory();
    c.Register<MyCommand, MyResult>();
});
```

</details>

<details><summary>Export OpenAPI documents as '.http' files</summary>

The **FastEndpoints.OpenApi** package can now export generated OpenAPI documents as REST Client `.http` files, alongside the existing `.json` export, for quick manual testing of endpoints without leaving the editor.

```csharp
await app.ExportHttpFilesAndExitAsync("v1"); // doc name should match .OpenApiDocument() config
```

```
dotnet run --export-http-files true
```

</details>

<details><summary>Skip executing endpoint handler if response already started</summary>

Endpoints can opt in to skip **HandleAsync**/ **ExecuteAsync** (and **OnAfterHandle**) when a response has already been started, for example from **OnBeforeHandle\***. Post-processors still run. Pre-processors already short-circuit without this setting.

```csharp
public override void Configure()
{
    Get("/resources");
    DontExecuteHandlerIfResponseStarted();
}

// or globally:
app.UseFastEndpoints(c => c.Endpoints.Configurator = ep =>
{
    ep.DontExecuteHandlerIfResponseStarted();
});
```

</details>

## Fixes 🪲

<details><summary>Attribute decorations are forwarded to every route of a multi-route endpoint</summary>

Class-level attributes that FastEndpoints doesn't interpret itself (such as `[EnableCors]`, `[EnableRateLimiting]`, `[RequestFormLimits]`, or your own metadata attributes) are forwarded to the mapped endpoint's metadata so that ASP.NET middleware can see them. On an attribute-configured endpoint declaring more than one route, only the first route received them.

```csharp
[HttpGet("orders/{id}", "purchases/{id}")] //the second route used to lose the attribute below
[EnableRateLimiting("fixed")]
sealed class MyEndpoint : EndpointWithoutRequest { ... }
```

Endpoints configured with a `Configure()` method were never affected. The endpoint definition is also no longer locked until all of its routes have been mapped.

</details>

<details><summary>Conditional FluentValidation presence rules no longer make OpenAPI properties required</summary>

`FastEndpoints.OpenApi` now preserves optional and nullable schema properties when `NotNull()` or `NotEmpty()` is guarded by a synchronous or asynchronous `When(...)`/`Unless(...)` condition.

Independent unconditional presence rules still mark the property as required and non-null as before.

</details>

<details><summary>Form file schemas are consistently emitted as binary in OpenAPI</summary>

`FastEndpoints.OpenApi` now emits `IFormFile` properties as `type: string` with `format: binary`, including items in `IFormFileCollection`, `IEnumerable<IFormFile>`, `List<IFormFile>`, and array schemas.

Suffixed or otherwise non-exact `IFormFile` schema references are also normalized before their components are removed, preventing dangling references in the generated document.

</details>

<details><summary>Nullable OpenAPI schemas with composition now emit valid null branches</summary>

`FastEndpoints.OpenApi` now emits valid OpenAPI 3.1 schemas for nullable arrays and nullable object references when composition keywords such as `oneOf` are involved.

Nullable arrays now inline the referenced array schema instead of combining `type: ["null", "array"]` with a non-null `oneOf`, and nullable object references now preserve null validity with an explicit null branch.

</details>

<details><summary>GET/HEAD root collection request bodies are optional in OpenAPI</summary>

`FastEndpoints.OpenApi` and `FastEndpoints.Swagger` now mark root collection request bodies (`List<T>` and `T[]`) as optional for `GET` and `HEAD` endpoints while preserving the generated array schema.

This matches runtime binding behavior where omitted `GET`/`HEAD` request bodies bind as empty collections, while non-collection request DTOs and other HTTP methods remain unchanged.

</details>

<details><summary>'415 Unsupported Media Type' responses for endpoints with implicitly-bound route params</summary>

Endpoints whose request DTO properties are bound to route values by name match alone (no `[RouteParam]` attribute) no longer receive a `415 Unsupported Media Type` response for `PUT`/`POST`/`PATCH` requests sent without a body or `Content-Type` header.

```csharp
public override void Configure() => Put("bookings/{BookingId}/pause");

public sealed class PauseBookingRequest
{
    public long BookingId { get; set; }
}
```

Previously, only properties decorated with `[RouteParam]` (or another attribute deriving from `NonJsonBindingAttribute`) were recognized as not requiring a JSON body, so a route-param-only DTO like the one above incorrectly demanded a `Content-Type` header.

</details>

<details><summary>Indexer properties no longer break request binding and validation</summary>

Public indexers are excluded from the bindable-property set (and from the reflection source generator cache), so types that declare one no longer throw when compiling getters/setters.

Previously an indexer was treated as a bindable property, which failed endpoint registration, nested `[FromForm]`/`[FromQuery]` binding, data-annotation validation recursion, and shapes such as `List<List<T>>` whose element type exposes `Item[int]`.

</details>

## Improvements 🚀

<details><summary>Configure versioned OpenAPI documents from <code>AddVersioning</code></summary>

On .NET 10, `AddVersioning` accepts an optional third argument of type `Action<VersionedOpenApiOptions>`, letting consumers configure the versioned OpenAPI documents from the `Asp.Versioning.OpenApi` package, whose per-version document services are required by `MapOpenApi().WithDocumentPerVersion()`.

```csharp
services.AddVersioning(
    o =>
    {
        o.ApiVersionReader = ApiVersionReader.Combine(new UrlSegmentApiVersionReader());
    },
    o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    },
    o => o.Document.AddDocumentTransformer(
        (document, _, _) =>
        {
            document.Info.Title = "My API";
            document.Info.Description = "My API description";
            return Task.CompletedTask;
        }));
```

The argument is optional, so existing callers of `AddVersioning` are unaffected. The new parameter is only available on .NET 10 targets because `Asp.Versioning.OpenApi` itself does not ship for earlier frameworks.

</details>

<details><summary>Kiota client generation uses Microsoft.OpenApi.Kiota.Builder 1.29.1</summary>

`FastEndpoints.OpenApi.Kiota` and `FastEndpoints.ClientGen.Kiota` now ship with `Microsoft.OpenApi.Kiota.Builder` **1.29.1**, a security-only backport that stays on the `Microsoft.OpenApi` 2.x line compatible with `Microsoft.AspNetCore.OpenApi`.

This release patches Kiota codegen injection and path-resolution CVEs without requiring a breaking OpenAPI 3.x bump. Newer Kiota 1.30+ builds remain incompatible until ASP.NET OpenAPI itself moves to OpenAPI 3.x.

</details>

<details><summary>Faster assembly scanning at startup</summary>

Reflection based type discovery no longer builds a LINQ set of the wanted interface types for every single type it inspects, which is the bulk of the work `AddFastEndpoints()` / `AddMessaging()` do at startup (measured on a 5 interface discovery: 180ns and 448 bytes per inspected type, down to 66ns and 35 bytes). The assembly exclusion list is also matched with an ordinal string comparison now, instead of a culture sensitive one that went through ICU collation per assembly.

</details>

<details><summary>Idempotency policy avoids a per-request LINQ scan of endpoint metadata</summary>

`IdempotencyPolicy` (registered as an ASP.NET output-cache base policy, so it runs on **every** request) no longer looks up the endpoint's `EndpointDefinition` with `Metadata.OfType<EndpointDefinition>().SingleOrDefault()`, which allocates an enumerator and walks the whole metadata collection. It now uses `Metadata.GetMetadata<EndpointDefinition>()`, the same cached-per-type lookup already used elsewhere in the request pipeline.

</details>

<details><summary>Response cache header value is built once per endpoint</summary>

The `Cache-Control` value of a response cached endpoint is no longer re-interpolated on every request. Both of its inputs (the configured `ResponseCacheLocation` and duration) are fixed after startup, so the value is now built on the first request to the endpoint and reused, removing a string interpolation plus an `int.ToString()` per request. The emitted headers are unchanged.

</details>

<details><summary>MCP addon targets Model Context Protocol SDK v2</summary>

`FastEndpoints.Mcp` now depends on `ModelContextProtocol.AspNetCore` **2.0** (2026-07-28 MCP spec). HTTP transport is stateless by default; existing `AddMcp()` / tool wiring is unchanged.

</details>

<details><summary>AccessControl group names resolve compile-time constants</summary>

The source generator that builds `Allow` permission groups from `AccessControl(...)` calls now accepts compile-time string constants for group names (`const` fields, `nameof(...)`, etc.), not only string literals.

```csharp
static class PermissionGroup
{
    internal const string Admin = nameof(Admin);
}

public override void Configure()
{
    Put("/inventory/manage/update");
    AccessControl("Inventory_Update_Item", PermissionGroup.Admin);
}
```

Previously, non-literal group arguments were ignored, so the generated permission was omitted from groups such as `Allow.Admin`.

</details>

<details><summary>Refresh token service support for union-type returning endpoints</summary>

A new `CreateTokenWith<TService, TTokenResponse>()` overload lets endpoints that return a union-type result (e.g. `Results<Ok<TokenResponse>, UnauthorizedHttpResult>`) create access/refresh token pairs, by decoupling the token response type from the endpoint's response type.

</details>

<details><summary>Frozen lookup caches for hot paths</summary>

Several read-mostly internal lookup tables now use `FrozenDictionary`/`FrozenSet` after startup construction, improving repeated lookup performance in request binding, access-control generation, and OpenAPI/Swagger metadata processing without changing public APIs.

Endpoint security policies now build a `FrozenSet` of allowed permissions/scopes/claim types once when the policy is constructed, instead of scanning the backing collection on every authorization check.

`RequestBinder<TRequest>` now indexes `[FromClaim]` / `[HasPermission]` properties once per DTO type and matches principal claims against those indices, instead of building per-request claim dictionaries or permission sets sized to the full principal.

</details>

<details><summary>Reduced allocations when sending byte arrays and empty json objects</summary>

`Send.BytesAsync()` no longer builds its own `async` state machine and `Task<Void>`, since it simply forwards to `Send.StreamAsync()`, which already owns disposal of the supplied stream.

`Send.EmptyJsonObject()` now serializes a shared empty `JsonObject` instead of allocating a new one per call. The response still goes through the configured response serializer, so custom serializer hooks are unaffected.

</details>

<details><summary>Relaxed agent name validation</summary>

A2A skill ids and MCP tool names now allow dots and forward slashes, so path/version-style identifiers such as `users/read.v1` can be published without renaming.

Some external MCP adapters may still apply OpenAI-style function-name validation and reject dots or slashes.

</details>

<details><summary>Reduced allocations when sending streams and files</summary>

`Send.StreamAsync()` and `Send.FileAsync()` no longer allocate a typed response-header wrapper when no `lastModified` value is supplied, no longer allocate an array to compute the request's precondition state, and no longer build a second stream-disposal state machine, since the stream is already disposed by `Send.StreamAsync()` itself.

</details>

<details><summary>Connection-level subscriber ids for remote event subscriptions</summary>

Remote connections can now set `SubscriberID` once and use it as the default subscriber id for event subscriptions on that connection.

```csharp
app.MapRemote("http://localhost:6000", c =>
{
    c.SubscriberID = "worker-a";
    c.Subscribe<SomethingHappened, WhenSomethingHappens>();
});
```

Subscription-specific ids still take precedence, so `SubscribeWithExplicitId(...)` can override the connection-level default when needed.

</details>

<details><summary>Unified complex form and query binding</summary>

Complex `[FromForm]` and `[FromQuery]` object binding now share a single recursive binder (`ComplexSourceBinder`) with small source adapters for form fields/files and query params. Behavior is unchanged for nested objects, collections, form files, and validation failures. The old split implementations are removed so edge-case fixes land in one place.

</details>

<details><summary>Cached metadata for complex form/query binding</summary>

`ComplexSourceBinder` now lazily caches per-property binding metadata (field names, type kind flags, setters, value parsers, and `List<T>` factories) on the existing reflection cache. Nested form and query graphs no longer re-run attribute lookup, type classification, or `MakeGenericType` on every request. Behavior is unchanged; only the hot path is cheaper after first use.

</details>

<details><summary>Shared service-resolver facade for endpoints, mappers, and validators</summary>

The eight `Resolve` / `TryResolve` / `CreateScope` pass-throughs that were duplicated on `Endpoint`, `Group`, mappers, `Validator`, `BinderContext`, and `HttpContext` extensions now live in a single Core type, `ServiceResolverClient` (with an internal static `Forward` helper for types that cannot inherit it).

Public resolve APIs and behavior are unchanged. Only the ownership of the default DI facade is centralized so future resolver changes land in one place.

</details>

<details><summary>Fewer allocations in complex query and form binding</summary>

Complex `[FromQuery]` / `[FromForm]` binding allocates less on each request:

- `ParentPrefixIndex` (used to skip nested objects with no matching keys) no longer materializes every parent path substring into a `HashSet`. It indexes spans into the original query/form keys in a single pass (capacity from key count, grows on unique prefixes only), keeping O (1) prefix checks without per-segment string allocations.
- Nested and indexed key construction (`prefix.field`, `key[i]`) uses `string.Concat` / `string.Create` instead of repeated interpolations.
- Complex collection binding stops at the first missing `items[i]` prefix without allocating empty element instances.

Behavior is unchanged for nested DTOs, collections, form files, and validation failures. The win shows up as lower gen-0 traffic on nested query/form graphs (for example the query-binding benchmark path).

</details>

<details><summary>One less allocation per request in the endpoint execution path</summary>

The internal `ValidationFailed` helper in the endpoint execution path no longer closes over the enclosing locals, which previously made the compiler heap allocate a closure on every request even though the helper only runs when binding or validation fails. Measured saving is 48 bytes per request on 64 bit. Behavior is unchanged.

</details>

<details><summary>Allocation-free event bus dispatch for the common case</summary>

`EventBus<TEvent>` no longer dispatches via `Parallel.ForEachAsync`; handlers are invoked directly and awaited with `Task.WhenAll`, with a single-handler fast path that is allocation-free for the default `WaitForAll` case. Multi-handler and `WaitForNone`/`WaitForAny` paths also allocate less; `PublishFilteredAsync` reuses the registered array for identity/empty filters. Handlers are no longer capped at `Environment.ProcessorCount` (all start immediately). `WaitForNone` remains fire-and-forget (offloaded).

</details>

<details><summary>Warmup precompiles complex form/query binding metadata</summary>

`Endpoints.Warmup()` now precompiles `ComplexSourceBinder` metadata (setters, parsers, factories, type flags) for the full object graph under each `[FromForm]` / `[FromQuery]` property, not only the request DTO's own properties and validation getters.

Roots are taken from the request DTO itself, so warmup still covers them when a custom `IRequestBinder<TRequest>` is registered. First-request lock contention on the per-property cache is avoided as a result.

</details>

## Minor Breaking Changes ⚠️

<details><summary>Nested collections are rejected in complex form/query binding</summary>

Complex `[FromForm]` / `[FromQuery]` binding now throws `NotSupportedException` when a collection's element type is itself a complex collection (e.g. `List<List<Item>>`, `List<byte[]>`, `List<Dictionary<TKey, TValue>>`), instead of returning an empty list or failing with an unrelated indexer error.

These shapes have no key convention and never bound data. Simple collections such as `List<string>` and `List<int>` are unaffected.

</details>