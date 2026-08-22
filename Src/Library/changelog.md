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

## Improvements 🚀

## Minor Breaking Changes ⚠️