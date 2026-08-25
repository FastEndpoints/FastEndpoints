---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

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

<details><summary>Nullable collection properties no longer get <code>const: null</code> in OpenAPI documents</summary>

`FastEndpoints.OpenApi` 8.3.0 emitted nullable collection properties with a sibling `"const": null`. JSON Schema applies keywords together, so only `null` validated; a populated array failed against the schema describing it.

The property now serializes as a nullable array only:

```json
"children": { "type": ["null", "array"], "items": { "$ref": "#/components/schemas/Child" } }
```

Visible with `Microsoft.OpenApi` 2.11.0 or later.

</details>

## Improvements 🚀

## Minor Breaking Changes ⚠️