---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Export OpenAPI documents as '.http' files</summary>

The **FastEndpoints.OpenApi** package can now export generated OpenAPI documents as REST Client `.http` files, alongside the existing `.json` export, for quick manual testing of endpoints without leaving the editor.

```csharp
await app.ExportHttpFilesAndExitAsync("v1"); // doc name should match .OpenApiDocument() config
```

```
dotnet run --export-http-files true
```

</details>

<details><summary>'FastEndpoints.CommandRules' package for rule-based command dispatch</summary>

A new `FastEndpoints.CommandRules` package is now available for turning arbitrary input into one or more commands using small, ordered rules.

It's useful when an application event, webhook payload, request DTO, or domain object needs to fan out into different command-bus actions without putting branching logic in endpoints or handlers. Rules evaluate the input, build a command plan, and the dispatcher executes the selected commands immediately or queues them as jobs.

```csharp
// map input model to a rule
bld.Services.AddCommandRules(o => o.Register<OrderPlaced, OrderPlacedRule>());

// define the rule to specify which commands should ececute
sealed class OrderPlacedRule : CommandRule<OrderPlaced>
{
    public override bool CanHandle(OrderPlaced input)
        => input.SendReceipt;

    public override IEnumerable<PlannedCommand> Build(OrderPlaced input)
    {
        yield return PlannedCommand.Create(new ReserveStock(input.OrderId));

        yield return new PlannedCommand(new SendReceipt(input.OrderId))
        {
            Mode = CommandDispatchMode.QueueAsJob
        };
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

## Fixes 🪲

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

## Improvements 🚀

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

<details><summary>Relaxed agent name validation</summary>

A2A skill ids and MCP tool names now allow dots and forward slashes, so path/version-style identifiers such as `users/read.v1` can be published without renaming.

Some external MCP adapters may still apply OpenAI-style function-name validation and reject dots or slashes.

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

[//]: # (## Breaking Changes ⚠️)