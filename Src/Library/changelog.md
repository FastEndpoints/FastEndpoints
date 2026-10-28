---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## Breaking Changes ⚠️

<details><summary>Target .NET 10 or .NET 11 when upgrading to FastEndpoints v9</summary>

Runtime packages, including the independently versioned MCP and A2A addons, now target only `net10.0` and `net11.0`. Retarget .NET 8/9 apps before upgrading, or keep your current FastEndpoints version.

The serializer-context-generator CLI requires .NET 10+. The Roslyn generator requires Roslyn 5.0, the .NET 10+ SDK, and Visual Studio 2026 18.0+ when using VS. Its `netstandard2.0` assets (and attributes) support compiler hosts, not older app runtimes. Upgrade SDKs/IDEs first.

.NET 11 support currently uses RC2 framework packages. Revalidate against the final .NET 11 SDK and packages before a stable release.
</details>

<details><summary>The ASP.NET API Versioning integration is limited to .NET 10</summary>

`FastEndpoints.AspVersioning` remains .NET 10-only: `Asp.Versioning.OpenApi` requires Microsoft.OpenApi 2.x, which is incompatible with the .NET 11 stack. On .NET 11, use built-in route-based versioning or defer upgrading to FE v9 until the integration supports it.

</details>

<details><summary>OpenAPI 3.1 schema examples now use <code>examples</code> arrays</summary>

`FastEndpoints.OpenApi` writes XML and generated polymorphic schema examples as `examples: [value]` in OpenAPI 3.1. Reference-local lists retain all values, including JSON nulls. OpenAPI 3.0 uses the first entry as singular `example`. Media-type, parameter, and header examples are unchanged.

Use `schema.Examples = [exampleNode]` in custom transformers and read `examples` in consumers. Deduplication and `.http` export also use `Examples` (export selects the first). Regenerate documents/clients and update snapshots/tooling expecting singular schema examples. HTTP payloads are unchanged.

</details>

<details><summary>OpenAPI request examples no longer create endpoint-specific schemas</summary>

`FastEndpoints.OpenApi` keeps `EndpointSummary.ExampleRequest` and `RequestExamples` only on request-body media types. Models/enums differing only in endpoint examples now share schemas; intentional XML/custom schema examples and real schema differences remain. HTTP payloads are unchanged.

Regenerate documents/clients and update references to removed duplicate types or changed method signatures. Read endpoint examples from the operation's request-body media-type `example` / `examples`, not schemas/properties. Use XML or transformers for shared schema examples; copying endpoint examples onto shared schemas can restore duplication or leak examples between endpoints.

</details>

<details><summary>OpenAPI security requirements use explicitly configured scopes</summary>

`FastEndpoints.OpenApi` now omits role names from `Roles()` and `[Authorize(Roles = "...")]` in operation security requirements. Previously, these names were merged with configured scopes, which could cause OAuth2/OpenID Connect clients to request invalid scopes. Runtime role enforcement is unchanged.

Applications that relied on role names being emitted as scopes must configure those scopes explicitly with the `globalScopeNames` argument of `DocumentOptions.AddAuth()`. Legacy `FastEndpoints.Swagger` behavior is unchanged.

</details>

<details><summary><code>Group.Configure()</code> is no longer overridable</summary>

`Configure()` is now non-virtual, avoiding constructor warnings. Route prefixes and group actions are unchanged, including parent-group execution after subgroup configuration.

Replace any `Group.Configure()` override with a constructor call to `Configure()`.
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

`Errors` is now a materialized `IReadOnlyCollection<Error>`, avoiding repeated error projection and naming-policy conversion when `AllowDuplicateErrors` is enabled. Deduplication behavior is unchanged.

Reading is unaffected. When assigning a plain `IEnumerable<Error>` to `Errors` (for example in a custom `ResponseBuilder`), materialize it first with `ToArray()` or `ToList()`.
</details>

<details><summary><code>Void</code> is now a struct instead of a class</summary>

`Void` (behind `ICommand` and `Task<Void>` send methods) is now a `readonly struct`. Synchronously completing no-result sends and command dispatches no longer allocate a `Task`.

This breaks `where TResult : class` over `ICommand<TResult>` (or `IServerStreamCommand<TResult>`) when `TResult` is `Void`. Drop the constraint, or add a sibling API constrained on `ICommand`.

</details>

## New 🎉

<details><summary>Opt-in event delivery acknowledgement for Remote Event Hubs</summary>

Remote Event Hubs can now wait for confirmation that a subscriber has stored an event before considering it delivered. If you need that extra assurance for your events, you can opt in with your storage providers. Existing providers continue to work as before.

See the documentation for setup instructions and things to keep in mind when switching over.

</details>

<details><summary>Financial-mode HTTP idempotency with <code>FinancialIdempotency()</code></summary>

If you're building payment-style endpoints where retrying a request shouldn't mean processing it twice, you can now opt in with `FinancialIdempotency()`. It protects requests while they're being processed and lets clients retry a successful request to get the original response back.

Register `AddFinancialIdempotency()` (in-memory store by default, or `AddFinancialIdempotency<TStore>()` for Redis/SQL) and `UseFinancialIdempotency()` after routing/auth, next to `UseOutputCache()`. Do not call both `Idempotency()` and `FinancialIdempotency()` on the same endpoint. Distributed stores must implement atomic `TryBegin`; in-flight keys return `409` so clients retry. Fail-closed `500` if a 2xx cannot be replayed. Requires an explicit trusted stable `CallerScope`. Ownership-token settlement prevents stale writers; active reservations never auto-expire. Uncertain (unreplayable) outcomes fail closed for `UnreplayableRetention` (default 24h via `DefaultUnreplayableRetention`, never shorter than `Duration`), after which a retry runs the handler again. Concurrent requests return immediate `409`. Exceptions and ambiguous non-2xx responses retain protection; only explicit no-side-effects rejection releases a key. Bounded lifecycle capture includes empty responses and pending pipe bytes. Uploaded file contents participate in the hash. Breaking store/identity changes require coordinated migration. A durable application store and transactional/downstream idempotency remain deployment obligations.

This is a separate mode from the existing `Idempotency()`, which continues to work as before. Here's a quick example to get started:

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

See the documentation for the full setup, storage options, and safeguards needed for production use.

</details>

<details><summary>Custom status codes for <code>Send.StreamAsync()</code>, <code>Send.BytesAsync()</code>, and <code>Send.FileAsync()</code></summary>

These three send methods now have overloads that take an HTTP status code. Existing calls keep returning `200`. Range processing and precondition checks can still replace the status with `206`, `304`, `412`, or `416`.

```csharp
await Send.StreamAsync(zipStream, statusCode: 207, fileName: "result.zip", contentType: "application/zip");
```

</details>

<details><summary>Describe MCP tool input and output properties with <code>[Description]</code></summary>

Annotate DTO properties with the standard `System.ComponentModel.DescriptionAttribute` to add descriptions to MCP tool schemas. Nested properties, JSON property names, naming policies, and endpoint serializer contexts are supported.

```csharp
public class Request
{
    [System.ComponentModel.Description("City name or zip code")]
    public string Location { get; set; }
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
<details><summary>Audit refused MCP tool calls without logging tool listings</summary>

You can now set `McpOptions.OnToolCallRefused` to record calls rejected by `ToolVisibilityFilter`. The callback receives the endpoint, caller, and HTTP context, and runs once per refused call. Routine tool listings and unknown tool names don't trigger it.

See [auditing refused MCP calls](https://fast-endpoints.com/docs/ai-agents#auditing-refused-mcp-calls) for an example.

</details>

## Fixes 🪲

<details><summary>Custom endpoint metadata is now visible during MCP and A2A calls</summary>

Pre-processors and other endpoint code can now read metadata added with `Definition.Metadata(...)` through `HttpContext.GetEndpoint().Metadata` during agent calls, just as they can during HTTP calls. This fixes metadata-based authorization checks that could incorrectly deny agent calls because their declarations appeared to be missing.

</details>

<details><summary>Custom DataAnnotations validators can resolve request-scoped services</summary>

Custom validation attributes can now resolve registered services through `ValidationContext.GetService()`. Validation uses the same request scope as your endpoint, including when validating nested objects and collection items.

</details>

<details><summary>Swagger UI shows the correct search hint after a slow load</summary>

The legacy <code>FastEndpoints.Swagger</code> search box now shows <code>Search...</code> even when the API definition takes longer to load. Full-text search remains available while the search box finishes rendering.

</details>

<details><summary>OpenAPI generation supports polymorphic properties with child validators</summary>

Fixed OpenAPI document generation throwing a <code>NotSupportedException</code> when a JSON-polymorphic request property has a FluentValidation child validator. Discriminator mappings are preserved when schemas are copied for endpoint-specific validation.

</details>

<details><summary>Preserve XML property examples on referenced OpenAPI schemas</summary>

XML property examples stay alongside `$ref` as `examples`, rather than leaking onto shared components. This covers date, time, enum, object, and collection properties, including operation-specific validator copies, which also retain XML summaries.

Custom reference examples support JSON null, including when nullable collections are inlined. Exported `.http` bodies prefer reference-local examples over component examples/defaults, including explicit nulls.

</details>

<details><summary>Read XML documentation in single-file apps</summary>

`FastEndpoints.OpenApi` now reads published XML files from the app directory for single-file apps, restoring missing summaries, remarks, and examples.

</details>

<details><summary>Preserve OpenAPI minimum lengths when combining validation rules</summary>

`NotEmpty().MaximumLength(50)` now documents `minLength: 1` instead of `0`. Combining `NotEmpty()` with minimum, bounded, or exact length rules preserves the stricter minimum regardless of rule order. The same minimum composition applies to array schemas through `minItems`. Runtime validation and maximum-bound handling are unchanged.

</details>

<details><summary>Honor endpoint request descriptions that match a referenced type's summary</summary>

Explicit request property descriptions configured with `Summary()` now override reference-local XML summaries even when the configured text matches the shared type's summary. Shared schemas and other endpoints retain their descriptions, and unchanged descriptions continue to reuse schemas.

</details>

<details><summary>Keep fallback input sources optional in OpenAPI documents</summary>

Optional headers and query parameters for optional claim/permission properties now remain optional when FluentValidation requires the final DTO value. Requests can supply that value through another binding source. Parameter schema constraints and explicitly required headers remain documented. `RemoveFromSchema` continues to control visibility independently of runtime binding.

</details>

<details><summary>OpenAPI applies <code>RuleForEach()</code> validators to collection items</summary>

`RuleForEach(x => x.Tags).NotEmpty().MaximumLength(20)` now constrains collection `items`, not the collection's requiredness, nullability, or size. Item length, numeric, and non-null constraints also work for nullable lists/arrays. `RuleFor()` still constrains the collection itself.

</details>

<details><summary>Nullable collection properties keep their XML summaries in OpenAPI documents</summary>

A nullable collection property such as `List<string>?` lost its XML summary in the generated OpenAPI document. Its description now appears alongside the `["null", "array"]` schema, the same as for non-nullable collections.

</details>

<details><summary>Schema extensions no longer fail OpenAPI generation for nullable references</summary>

A schema transformer that added an extension such as `x-enumDescriptions` to a component made document generation throw `NotSupportedException` when a nullable property referenced that component (for example `Status?`). Extensions are now copied with the schema.

</details>

<details><summary>Multi-line XML summaries no longer keep source indentation in OpenAPI descriptions</summary>

`FastEndpoints.OpenApi` now removes the source indentation from multi-line XML `<summary>` and `<remarks>` text. Paragraphs and lists in a description render as markdown instead of a code block. Relative indentation for nested list items and indented code blocks is preserved, along with trailing spaces used for Markdown hard line breaks.

</details>

<details><summary>Preserve XML property summaries on referenced OpenAPI schemas</summary>

`FastEndpoints.OpenApi` now keeps each DTO property's XML summary alongside its `$ref`, including object, collection, and enum properties. Shared component descriptions retain the type's own summary. Properties with inline schemas continue to use their own descriptions.

</details>

<details><summary>OpenAPI documents generate for abstract polymorphic responses</summary>

With <code>UseOneOfForPolymorphism</code> enabled, an abstract <code>[JsonPolymorphic]</code> response no longer fails document generation. The <code>oneOf</code> entries now reference the same derived schemas as the discriminator mapping, and the framework <code>anyOf</code> list is removed from that schema.

</details>

<details><summary>Nested group tags are preserved in generated OpenAPI operations</summary>

Explicit <code>WithTags</code> labels from a root group, nested groups, and the endpoint are all written to the operation. Disabling automatic tagging, or calling <code>DontAutoTag ()</code>, removes only the route-derived tag. Repeated names are emitted once, and names that differ only by case stay distinct.

</details>

<details><summary>Honor <code>RemoveFromSchema</code> for claim and permission query parameters</summary>

Both `FastEndpoints.OpenApi` and `FastEndpoints.Swagger` now omit query parameters for optional `[FromClaim]` and `[HasPermission]` properties with `RemoveFromSchema = true`. Request binding is unchanged. `[FromHeader]` and `[FromCookie]` parameters remain documented when this flag removes their body properties.

</details>

<details><summary>OpenAPI enum properties honor their JSON converters</summary>

Property-level `[JsonConverter]` attributes now determine enum schema values and types, including string-enum naming policies and nullable enums. Converter-backed properties are documented inline, allowing other properties of the same enum type to keep their own serialization representation.

</details>

<details><summary>Reflection generation succeeds in projects without non-generic classes</summary>

Projects containing only records, open generic classes, or no classes no longer fail with `CS8785`. They generate an empty `AddFrom...` method returning the cache, with namespace/method names still derived from the assembly name.

</details>

<details><summary>Comma-separated enum names no longer bind as a different member</summary>

Query, route, form, header, cookie, and claim binding now reject comma-separated/repeated names for non-`[Flags]` enums by default, rather than OR-ing them into another member (for example `Monday,Tuesday` into `Wednesday`). `[Flags]` combinations still bind when defined. Set `Binding.AllowUndefinedEnumValues = true` to restore raw `Enum.TryParse` behavior. JSON bodies still follow serializer options.

</details>

<details><summary>Validators nested inside an open generic no longer break <code>DiscoveredTypes</code> generation</summary>

Types nested inside open generics, such as `BaseValidator<T>.ChildValidator`, are now skipped, matching reflection discovery, instead of generating uncompilable code or failing for private types. Move them outside the open generic to register them.

</details>

<details><summary>No-result command middleware runs under Native AOT</summary>

Native AOT no-result command pipelines now use `AddCommandMiddleware` registrations instead of skipping middleware after the `Void` struct change. Closed `Register<MyCommand, Void, MyMiddleware>()` works unchanged. Open-generic `Register(typeof(Foo<,>))` remains unsupported for these commands and now throws instead of silently skipping.

</details>

<details><summary>Void-result job queues and command execution work under Native AOT again</summary>

Fixed Native AOT startup crashes in `UseJobQueues()` and execution failures in `ICommand.ExecuteAsync()` / command-rules `ExecuteNow` caused by the `Void` struct change. No-result queues and command executors now avoid unsupported generic value-type construction. No consumer changes are required.

</details>

<details><summary>Serializer context generation now fully qualifies enum type arguments</summary>

Generated `[JsonSerializable]` attributes now fully qualify enum generic arguments (for example in `Dictionary<MyEnum, MyDto[]>`), fixing `CS0246` compilation errors and `SYSLIB1030` missing-metadata warnings.

</details>

<details><summary>Overlapping OpenAPI document requests no longer throw or return a truncated spec</summary>

Concurrent `/openapi/*.json` requests now use isolated generation state, preventing `IndexOutOfRangeException`, `Collection was modified`, and missing paths. `MapOpenApi()` still rebuilds per request without serializing generation.

</details>

<details><summary>Singleton validators, mappers, processors and event handlers no longer capture scoped services from the first request</summary>

Cached singletons are now always built from the root provider, matching `Warmup()`, instead of capturing the first request's scoped dependencies. Scoped constructor injection now throws with scope validation enabled (the Development default). Resolve scoped services per request with `Resolve<T>()` or a new scope instead.

</details>

<details><summary>JWT revocation middleware now matches the <code>Bearer</code> scheme case-insensitively</summary>

`JwtRevocationMiddleware` now matches `Bearer` case-insensitively, like JWT authentication. Alternate casing such as `Authorization: bearer <jwt>` no longer bypasses `JwtTokenIsValidAsync()`.

</details>

<details><summary>JWT revocation middleware now checks alternate token sources after routing</summary>

After routing, revocation checks now include query/cookie tokens accepted through `JwtBearerEvents.OnMessageReceived`. Checks use the endpoint's effective policy schemes, or the default authenticate scheme when none are specified. Unrelated JWT schemes are skipped; authentication results are cached per request to avoid double validation.

`AddAuthenticationJwtBearer()` now enables the required `SaveToken` option. With direct `AddJwtBearer()` registration, set `SaveToken = true` for alternate token sources.

Before routing, header tokens are now checked instead of skipping the request, but alternate sources are not. Place `UseJwtRevocation<T>()` after `UseRouting()` for query/cookie checks and anonymous-endpoint handling. Matched anonymous endpoints remain exempt.

</details>

<details><summary>Routeless test helpers now URL-encode route parameter values</summary>

Route parameter values generated by `GetTestUrlFor()` are now URL-encoded, preventing reserved characters such as `#`, `?`, and spaces from truncating or altering the request URL.

</details>

<details><summary>Nullable collection properties no longer get <code>const: null</code> in OpenAPI documents</summary>

Removed the sibling `const: null` emitted by `FastEndpoints.OpenApi` 8.3.0, which incorrectly rejected populated arrays. Properties now use only the nullable array schema (`type: ["null", "array"]` with `items`). Affects `Microsoft.OpenApi` 2.11.0+.

</details>

## Improvements 🚀

<details><summary>Use the .NET 11 OpenAPI stack and newer Kiota client generation on .NET 11</summary>

`FastEndpoints.OpenApi` and `FastEndpoints.OpenApi.Kiota` use ASP.NET Core OpenAPI 11 and Microsoft.OpenApi 3.x when targeting .NET 11, with OpenAPI 3.2 documents and `QUERY` operation support by default. .NET 10 retains the compatible OpenAPI 10 / Microsoft.OpenApi 2.x stack and Kiota 1.29.2.

</details>

<details><summary>Warmup no longer precompiles the data-annotations validation graph when it's disabled</summary>

`Warmup()` now skips precompiling request DTO data-annotations graphs when `Validation.EnableDataAnnotationsSupport` is disabled (the default), reducing startup work.
</details>

<details><summary>SSE <code>StreamItem.Id</code> is now settable after construction</summary>

Assign `StreamItem.Id` after construction without cloning the item. `EventName`, `Data`, and `Retry` remain `init`-only.

</details>

<details><summary>FluentValidation rules now apply to OpenAPI query, path, header, and cookie parameters</summary>

`FastEndpoints.OpenApi` now documents requiredness, lengths, patterns, and numeric ranges on DTO-bound parameters, including bodyless GET/HEAD and mixed-source POST requests. Conditional rules behave as for request bodies. Generated clients now see these constraints.

</details>

<details><summary>Route mapping no longer rebuilds authorization metadata once per HTTP verb</summary>

Authorization metadata is now built once per endpoint and reused across its verbs/routes, or skipped entirely when all verbs are anonymous.
</details>

<details><summary>Command execution no longer builds a handler-interface <code>Type</code> it doesn't need on the hot path</summary>
Command and stream-command dispatch now construct handler-interface types only for first-time generic discovery or test-handler substitution, removing unnecessary `MakeGenericType` calls from normal execution.
</details>

<details><summary>Required-property validation no longer rebuilds a hash set per request</summary>
Required-property checks now reuse the set populated during binding instead of allocating another hash set per request. Validation behavior and case-insensitive matching are unchanged.
</details>
