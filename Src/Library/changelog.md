---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>New 'FastEndpoints.OpenApi' package based on 'Microsoft.AspNetCore.OpenApi'</summary>

Starting with `v8.2`, the FastEndpoints ecosystem has switched from `NSwag/Newtonsoft` based Swagger/OpenAPI document generation to the more modern and Native AOT friendly `Microsoft.AspNetCore.OpenApi` based document generation library. Integration is provided via a new `FastEndpoints.OpenApi` package which corrects a few issues with the MS package as well as doing a lot of post-processing on the document model to bring feature parity with the `FastEndpoints.Swagger` package.

There's no immediate need for you to switch to the new package if your projects are heavily invested in `NSwag` based generation. Especially if you're not yet on .NET10. The new package only supports .NET10+ projects. See EOL notice below for more info.

</details>

<details><summary>Streaming command handlers for the command bus</summary>

The in-process command bus can now execute commands that return `IAsyncEnumerable<T>` streams by implementing `IStreamCommand<TResult>` and `IStreamCommandHandler<TCommand, TResult>`.

Streaming commands use the same `ExecuteAsync()` extension method as regular commands, support their own middleware pipeline via `IStreamCommandMiddleware<TCommand, TResult>`, and can be used with closed or generic command handler registrations.

</details>

<details><summary>x402 Payment support for endpoints</summary>

Endpoints can now require x402 payments by calling `RequirePayment(...)` inside `Configure()`.

Global x402 defaults are configured with `builder.AddX402()` and `app.UseX402(...)`, and the middleware only runs for endpoints that opt in. The initial release supports the `exact` scheme with a single accepted payment option per endpoint and uses the safer default flow of verifying first, executing the handler, and settling only after a successful response.

</details>

<details><summary>New 'FastEndpoints.Mcp' and 'FastEndpoints.A2A' agent integration packages</summary>

Two new beta packages are now available for exposing your existing FastEndpoints endpoints to AI agent runtimes without having to build separate agent-specific controllers or handlers.

`FastEndpoints.Mcp` exposes opt-in endpoints as Model Context Protocol (MCP) tools over HTTP using the official MCP ASP.NET Core transport. `FastEndpoints.A2A` exposes opt-in endpoints as A2A skills with an agent card and JSON-RPC `SendMessage` dispatcher.

Both addons execute the normal FastEndpoints pipeline in-process, including binding, validation, pre/post processors and response serialization. Nothing is exposed by default. Endpoints must explicitly opt in via `this.McpTool(...)`, `[McpTool]`, `this.A2ASkill(...)`, or `[A2ASkill]`, and each package has separate agent-facing visibility filters so REST authorization and agent visibility can be configured independently.

```csharp
builder.Services
       .AddFastEndpoints()
       .AddMcp()
       .AddA2A();

app.UseFastEndpoints()
   .UseMcp()
   .UseA2A();
```

</details>

<details><summary>Generate a hydrated test URL from an endpoint type and request DTO</summary>

Testing extensions now expose `GetTestUrlFor<TEndpoint>(object request)` publicly. This lets you resolve the final routeless test URL for an endpoint without actually sending the request. Route parameters are populated from the supplied DTO instance, query string values are appended automatically, and the method also works in Aspire-style black-box tests by loading the endpoint URL cache over HTTP when necessary.

This is useful when you want to inspect or assert the exact URL that a test request would use before calling `GETAsync()`, `POSTAsync()`, etc.

```csharp
var request = new UpdateInvoiceRequest
{
    InvoiceId = 123,
    IncludeLines = true
};

var url = client.GetTestUrlFor<UpdateInvoiceEndpoint>(request);

url.ShouldBe("api/invoices/123?IncludeLines=true");
```

</details>

<details><summary>'ProblemDetails' support for the built-in exception handler</summary>

The built-in exception handler can now emit the RFC9457 compatible `ProblemDetails` response shape for unhandled exceptions by enabling `useProblemDetails` when registering it.

This lets validation failures and unhandled exception responses share the same standards compliant error contract when the app is configured with `c.Errors.UseProblemDetails()`.

```csharp
app.UseDefaultExceptionHandler(useProblemDetails: true)
   .UseFastEndpoints(c => c.Errors.UseProblemDetails());
```

</details>

<details><summary>Partial static properties in a partial 'Allow' classes are now source generated</summary>

You can now declare `public static partial string` properties in a `partial Allow` class without providing an implementation. The `FastEndpoints.Generator` package detects these declarations and generates the property body with the same computed permission code that would be produced if the permission were defined directly on an endpoint via `AccessControl(...)`.

```csharp
public static partial class Allow
{
    public static partial string Inventory_Create { get; }
    public static partial string Inventory_Update { get; }
}
```

The generator emits the implementations automatically:

```csharp
public static partial string Inventory_Create { get => "A1B"; }
public static partial string Inventory_Update { get => "C3D"; }
```

The generated permission codes are stable (derived from the property name via a SHA256-based hash), so they survive refactors as long as the property name stays the same.

</details>

<details><summary>Supply source generated type lists directly to startup configuration methods</summary>

`AddFastEndpoints`, `AddMessaging`, and `AddJobQueues` now each have a new overload that accepts one or more `List<Type>` values (one per referenced assembly), making it possible to use the `FastEndpoints.Generator` package with the Messaging as well as the JobQueue package when the main FastEndpoint package is not used.

**Before:**

```csharp
builder.Services.AddFastEndpoints(o =>
{
    o.SourceGeneratorDiscoveredTypes.AddRange(MyAssembly.DiscoveredTypes.All);
});
```

**After:**

```csharp
// single assembly
builder.Services.AddFastEndpoints(DiscoveredTypes.All);

// multiple assemblies
builder.Services.AddFastEndpoints(Lib1.DiscoveredTypes.All, Lib2.DiscoveredTypes.All);
```

The same pattern applies when using Messaging or JobQueues as standalone libraries (i.e. without `AddFastEndpoints`):

```csharp
// Messaging only
builder.Services.AddMessaging(DiscoveredTypes.All);

// JobQueues only
builder.Services.AddJobQueues<Job, JobStorage>(DiscoveredTypes.All);
```

The two overloads are intentionally separated since source-generated and reflection-based discovery are mutually exclusive strategies and prevents accidental use of both paths at once.

If `AddFastEndpoints` is already called with the discovered types, `AddMessaging` and `AddJobQueues` do not need them passed again. Use the parameterless overloads for those calls.

</details>

<details><summary>Keyed service support for source generated endpoint property injection</summary>

Endpoint properties decorated with `[KeyedService]` are now supported when using source generated reflection metadata.

The generator records the keyed-service key for endpoint property injection, allowing generated reflection caches to resolve those properties with `GetRequiredKeyedService(...)` instead of falling back to the unkeyed service registration.

</details>

<details><summary>Opt-in startup warmup for endpoints, validators, mappers, and event buses</summary>

Call `Warmup()` from `UseFastEndpoints(...)` to eagerly initialize validators, mappers, request binders, and compiled property setter delegates so the first real requests do not pay the cold-start cost.

Warmup can be scoped to a subset of endpoints with the optional filter argument:

```csharp
app.UseFastEndpoints(c =>
{
    c.Endpoints.Warmup(def => def.EndpointType.Namespace?.StartsWith("MyApp.CriticalEndpoints") is true);
});
```

When a filter is not provided, all registered endpoints are warmed up after `Warmup()` is called. Pass `_ => false` to skip endpoint warmup entirely.

Messaging warmup is also opt-in. Call `Warmup()` from `UseMessaging(...)` to eagerly resolve event bus instances:

```csharp
app.Services.UseMessaging(o => o.Warmup());
```

Job queue startup can request the same messaging warmup through `UseJobQueues(...)`:

```csharp
app.UseJobQueues(o => o.Warmup());
```

</details>

## Fixes 🪲

<details><summary>'ValidationSchemaProcessor' concurrency issue when generating multiple Swagger documents</summary>

The `_childAdaptorValidators` instance field on the singleton `ValidationSchemaProcessor` was a plain non-thread-safe `Dictionary<string, IValidator>`. Concurrent Swagger document generation (e.g. multiple documents being built in parallel at startup) could cause race conditions on that shared dictionary. The field is now a local variable scoped to each `Process` call, eliminating shared mutable state entirely.

</details>

<details><summary>Asymmetric JWT signing key updates no longer leak RSA handles</summary>

Updating asymmetric JWT signing keys at runtime no longer keeps undisposed RSA instances alive after each key rotation. The validation key is now built from exported public key parameters instead of holding on to the temporary RSA instance used for import, avoiding unmanaged crypto handle leaks without disposing keys that may still be in use by concurrent token validations.

</details>

<details><summary>'DateOnly','TimeOnly' not binding correctly from nested '[FromQuery]' DTOs</summary>

Complex query/form binding now treats `DateOnly`, `TimeOnly`, `Half`, `Int128`, `UInt128`, `BigInteger`, `IPAddress`, and `IPEndPoint` as scalar values instead of recursively binding them as complex objects.

This fixes nested `[FromQuery]` request DTO properties such as `DateOnly?` and `TimeOnly?` being left at their default values (`0001-01-01`, `00:00:00`, etc.) even when valid query string values were supplied.

</details>

<details><summary>'FastEndpoints.Swagger' nested validator issue</summary>

When a request DTO used a child validator (via `SetValidator(...)` / `RuleFor(x => x.Child).SetValidator(...)`), the property-level constraints defined in that child validator (such as `NotEmpty`, `MaxLength`, `InclusiveBetween`, etc.) were silently dropped from the generated Swagger schema for any request type processed after the first one that shared the same nested validator type.

The root cause was that `ValidationSchemaProcessor` kept a `_childAdaptorValidators` dictionary as a singleton-level instance field. This dictionary is used to track already-seen child validator types to prevent infinite recursion. Because it was shared across all schema processing calls, a nested validator type recorded while processing request type `A` would be treated as "already seen" when encountered again during processing of request type `B`, and its rules would not be applied to `B`'s schema.

</details>

## Improvements 🚀

<details><summary>Job queue scheduling now rejects impossible execution windows</summary>

Queued jobs now fail fast with an `ArgumentException` when the effective expiration time is not later than the scheduled execution time. This includes the default 4-hour expiration window, preventing deferred jobs from being stored with an empty eligibility window where they could never be picked up for execution before becoming stale.

</details>

<details><summary>Serializer options are now configured once per process during startup</summary>

FastEndpoints now performs its process-wide serializer configuration under a one-time startup lock. This prevents parallel host creation in the same process, such as `WebApplicationFactory` based integration tests, from re-pointing and mutating the shared `JsonSerializerOptions` instance while another host is already serializing requests or responses.

This keeps the existing process-global configuration model: the first FastEndpoints startup configures the shared serializer options and later hosts in the same process reuse that configuration instead of reconfiguring it.

</details>

<details><summary>Access control permission lookup data is now source generated</summary>

The `Allow` source generator now emits the permission name/code lookup initialization directly, avoiding runtime reflection when access control permissions are first used.

</details>

<details><summary>Source generated reflection data now avoids object factories for partial request DTOs</summary>

The reflection source generator no longer emits direct object factory expressions for `partial` request DTO types. This avoids compile-time failures when another source generator augments the same partial type into an abstract base type, such as discriminated-union libraries that generate abstract union roots and concrete nested variants.

Property metadata is still generated for those DTOs, so binding and validation can continue using source generated reflection data without trying to instantiate a type that may become abstract later in the generator pipeline.

</details>

<details><summary>Deeply nested [FromForm] DTOs with files now work with routeless test helpers</summary>

The routeless `HttpClient` testing extensions can now build `multipart/form-data` requests from nested `[FromForm]` DTOs containing `IFormFile`, file collections, scalar collections, and complex child objects. Root `[FromForm]` wrappers are still promoted to top-level form fields, while nested values use the dotted/indexed field names expected by the binder, such as `Details.Image` and `Items[0].Attachment`.

The generated form data also respects binding source metadata by skipping route, query, header, cookie, claim, permission, and `[DontBind]` fields that should not be sent as form fields. Circular object graphs are detected up front and fail with a clear `NotSupportedException` instead of recursing forever.

</details>

<details><summary>More lenient route prefix handling</summary>

Global route prefixes now normalize empty-string configuration values to no prefix and trim surrounding `/` characters before route registration. This fixes cases where configuration APIs return `""` instead of `null` while preserving the existing `RoutePrefixOverride(...)` contract, including ignoring endpoint-level overrides when no global prefix is configured and allowing `RoutePrefixOverride(string.Empty)` to disable the global prefix for a single endpoint.

</details>

<details><summary>Configurable response deserialization behavior for routeless test helpers</summary>

The routeless `HttpClient` testing extensions no longer hardcode `JsonUnmappedMemberHandling.Disallow` when deserializing response DTOs. You can now control that behavior with `c.Serializer.TestResponseUnmappedMemberHandling`, while defaulting to strict failure when the response JSON contains properties your test DTO does not define.

To allowing unmapped members during test response deserialization:

```csharp
using System.Text.Json.Serialization;

app.UseFastEndpoints(c =>
{
    c.Serializer.TestResponseUnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
});
```

</details>

<details><summary>Emit wrapped JSON Patch request bodies as top-level patch operation arrays in OpenApi docs</summary>

When a request DTO contains a `[FromBody]` property accepted as `application/json-patch+json`, the generated OpenAPI request body schema is now promoted from the wrapper object shape to the top-level JSON Patch array shape expected by Swagger UI.

This improves the generated example payload from:

```json
{
  "operations": [
    {
      "path": "string",
      "op": "string",
      "from": "string",
      "value": "string"
    }
  ]
}
```

to:

```json
[
  {
    "path": "string",
    "op": "string",
    "from": "string",
    "value": "string"
  }
]
```

</details>

<details><summary>Undefined enum values are now rejected by default during non-JSON model binding</summary>

Route/query/form/header/cookie/claim binding now rejects enum values that are not explicitly defined by the target enum type. This closes a gap where numeric inputs such as `99` could be parsed into an enum even when that numeric value was not a declared member.

If you need to disable the new behavior and allow undefined enum values again, enable `AllowUndefinedEnumValues` explicitly:

```csharp
app.UseFastEndpoints(c =>
{
    c.Binding.AllowUndefinedEnumValues = true;
});
```

</details>

<details><summary>Malformed multipart form requests now fail as validation errors instead of server errors</summary>

Multipart form binding now treats request body parsing failures caused by malformed client input as validation failures. Truncated multipart bodies and invalid `Content-Disposition` headers are caught when no custom `FormExceptionTransformer` is configured, returning a `400 Bad Request` response instead of surfacing as unhandled `500 Internal Server Error` exceptions.

This avoids noisy false-positive security scanner findings and keeps genuinely unexpected exceptions propagating normally, while still allowing applications with a custom `FormExceptionTransformer` to control their own form parsing error behavior.

</details>

<details><summary>'Send.OkAsync(ct)' no longer tries to serialize cancellation tokens on untyped endpoints</summary>

When an endpoint response type is `object` such as with `EndpointWithoutRequest`, calling `Send.OkAsync(ct)` could previously bind to the response-body overload and end up passing the `CancellationToken` to STJ for serialization.

That case is now detected at runtime and routed to the no-body `200 OK` path instead, avoiding the serialization failure while preserving the existing overload surface.

```csharp
public class PingEndpoint : EndpointWithoutRequest
{
    public override void Configure()
        => Get("/ping");

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}
```

</details>

## End-Of-Life Packages 🪦

<details><summary>NSwag based Swagger packages are set to be discontinued</summary>

The following `NSwag` based packages will no longer be receiving new features:

- `FastEndpoints.Swagger` (main swagger doc generation library)
- `FastEndpoints.ClientGen` (NSwag based api client generation)
- `FastEndpoints.ClientGen.Kiota` (NSwag+Kiota based api client generation)

There is no immediate need for you to migrate away from these packages to the `Microsoft.AspNetCore.OpenApi` based new ones, as they will continue to receive bug fixes for the time being. Migration is not that difficult either, as the new packages purposefully contain extremely close API surfaces. If you have no deep customization with stuff like custom newtonsoft converters, operation/schema processors, etc; migration should not be too difficult.

The new `FastEndpoints.OpenApi*` packages are however .NET 10+ only. If you'd like to migrate to the new packages, you'd have to migrate your projects to .NET 10 first. The above discontinued packages will be deprecated after .NET 9 goes out of support.

</details>

## Minor Breaking Changes ⚠️

<details><summary>Dropped support for netstandard2.1</summary>

`FastEndpoints.Core`, `FastEndpoints.Messaging.Core`, and `FastEndpoints.Messaging.Remote.Core` no longer target `netstandard2.1` and now support `net8.0+` only.

If you consume these packages from shared libraries or older applications, retarget those projects to `net8.0` or newer before upgrading.

</details>

<details><summary>'SourceGeneratorDiscoveredTypes' removed from 'EndpointDiscoveryOptions'</summary>

The `EndpointDiscoveryOptions.SourceGeneratorDiscoveredTypes` property has been removed. Pass the source-generated type list directly to `AddFastEndpoints` instead — see above.

```csharp
// before
builder.Services.AddFastEndpoints(o =>
{
    o.SourceGeneratorDiscoveredTypes.AddRange(DiscoveredTypes.All);
});

// after
builder.Services.AddFastEndpoints(DiscoveredTypes.All);
```

</details>

<details><summary>Undefined enum values are no longer accepted by default for non-STJ model binding</summary>

The default behavior for non-JSON model binding has changed. Previously, enum values were accepted as long as `Enum.TryParse()` succeeded, which meant undefined numeric values such as `99` could still bind successfully. The new default rejects enum values unless they are explicitly defined by the target enum type.

This may break existing endpoints that rely on accepting undefined numeric enum values from route/query/form/header/cookie/claim inputs.

Do the following to revert to the previous behavior:

```csharp
app.UseFastEndpoints(c =>
{
    c.Binding.AllowUndefinedEnumValues = true;
});
```

</details>