---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Support for .Net 8.0</summary>

todo: write description

</details>

<details><summary>Simpler way to register Pre/Post Processors with DI support</summary>

ref: https://github.com/FastEndpoints/FastEndpoints/pull/528

</details>

<details><summary>Exception handling capability for Post-Processors</summary>

todo: update doc page and link here

</details>

<details><summary>Shared state support for global Pre/Post Processors</summary>

ref: https://github.com/FastEndpoints/FastEndpoints/pull/523

</details>

<details><summary>Ability to hide the error reason in the JSON response when using the default exception handler middleware</summary>

todo: write description

</details>

[//]: # (## Improvements 🚀)

## Fixes 🪲

<details><summary>Auto binding collections of form files fails after first request</summary>

An object disposed error was being thrown in subsequent for file collection submissions due to a flaw in the model binding logic, which has now been corrected.

</details>

<details><summary>Incorrect validation error field names for nested request DTO classes with [FromBody] attribute</summary>

todo: write description
ref: https://discord.com/channels/933662816458645504/1168177198415482972

</details>

<details><summary>Test assertions couldn't be done on ProblemDetails DTO</summary>

The `ProblemDetails` DTO properties had private setter properties preventing STJ from being able to deserialize the JSON which has now been corrected.

</details>

## Minor Breaking Changes ⚠️

<details><summary>Pre/Post Processor interface changes</summary>

todo: describe the change and how to migrate

</details>

<details><summary>AddJWTBearerAuth() default claim type mapping behavior change</summary>

The `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` static dictionary is presently used by ASP.NET for mapping claim types for inbound claim type mapping. In most
cases people use `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` to not have long claim types such
as `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` as the claim type for the `sub` claim for example.

The default behavior has now been changed to make the claim types not use the SOAP type identifies like above. If for some reason you'd like to revert to the old
behavior, it can be achieved like so:

```csharp
.AddJWTBearerAuth("jwt_signing_key", o =>
{
    o.MapInboundClaims = true;
});
```

See #526 for more info.

</details>