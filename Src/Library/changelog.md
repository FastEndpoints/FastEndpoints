---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>New 'FastEndpoints.OpenApi' package that uses 'Microsoft.AspNetCore.OpenApi'</summary>

Starting with `v8.2`, the FastEndpoints ecosystem has switched from `NSwag/Newtonsoft` based Swagger/OpenAPI document generation to the more modern and Native AOT friendly `Microsoft.AspNetCore.OpenApi` based document generation library. Integration is provided via a new `FastEndpoints.OpenApi` package which corrects a few issues with the MS package as well as doing a lot of post-processing on the document model to bring feature parity with the `FastEndpoints.Swagger` package.

There's no immediate need for you to switch to the new package if your projects are heavily invested in `NSwag` based generation. Especially if you're not yet on .NET10. The new package only supports .NET10+ projects. See EOL notice below for more info.

</details>

<details><summary>x402 Payment support for endpoints</summary>

Endpoints can now require x402 payments by calling `RequirePayment(...)` inside `Configure()`.

Global x402 defaults are configured with `builder.AddX402()` and `app.UseX402(...)`, and the middleware only runs for endpoints that opt in. The initial release supports the `exact` scheme with a single accepted payment option per endpoint and uses the safer default flow of verifying first, executing the handler, and settling only after a successful response.

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

## Fixes 🪲

<details><summary>'DateOnly','TimeOnly' not binding correctly from nested '[FromQuery]' DTOs</summary>

Complex query/form binding now treats `DateOnly`, `TimeOnly`, `Half`, `Int128`, `UInt128`, `BigInteger`, `IPAddress`, and `IPEndPoint` as scalar values instead of recursively binding them as complex objects.

This fixes nested `[FromQuery]` request DTO properties such as `DateOnly?` and `TimeOnly?` being left at their default values (`0001-01-01`, `00:00:00`, etc.) even when valid query string values were supplied.

</details>

## Improvements 🚀

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