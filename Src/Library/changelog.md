---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>New 'FastEndpoints.OpenApi' package that uses 'Microsoft.AspNetCore.OpenApi'</summary>


TODO: write docs + description here

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

<details><summary>x402 Payment support for endpoints</summary>

Endpoints can now require x402 payments by calling `RequirePayment(...)` inside `Configure()`.

Global x402 defaults are configured with `builder.AddX402()` and `app.UseX402(...)`, and the middleware only runs for endpoints that opt in. The initial release supports the `exact` scheme with a single accepted payment option per endpoint and uses the safer default flow of verifying first, executing the handler, and settling only after a successful response.

</details>

## Fixes 🪲

## Improvements 🚀

<details><summary>Swagger now emits wrapped JSON Patch request bodies as top-level patch operation arrays</summary>

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