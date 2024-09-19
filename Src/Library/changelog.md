---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Job Queuing support for Commands that return a result</summary>

todo: update docs + describe here

</details>

<details><summary>Transform FluentValidation error property names with 'JsonPropertyNamingPolicy'</summary>

A new configuration setting has been introduced so that deeply nested request DTO property names can be transformed to the correct case using the `JsonPropertyNamingPolicy` of the application.

```csharp
app.UseFastEndpoints(c => c.Validation.UsePropertyNamingPolicy = true)
```

The setting is turned on by default and can be turned off by setting the above boolean to `false`.

</details>

## Improvements 🚀

<details><summary>Make Pre/Post Processor Context's 'Request' property nullable</summary>

Since there are certain edge cases where the `Request` property can be `null` such as when STJ receives invalid JSON input from the client and fails to successfully deserialize the content. Even in those cases, pre/post processors would be executed where the pre/post processor context's `Request` property would be null. This change would allow the compiler to remind you to check for null if the `Request` property is accessed from pre/post processors.

</details>

## Fixes 🪲

<details><summary>Nullable 'IFormFile' handling issue with 'HttpClient' extensions</summary>

The `HttpClient` extensions for integration testing was not correctly handling nullable `IFormFile` properties in request DTOs when automatically converting them to form fields, which has now been remedied.

</details>

<details><summary>Swagger processor issue with virtual path routes</summary>

The swagger processor was not correctly handling routes if it starts with a `~/` (virtual path that refers to the root directory of the web application), which has now been fixed.

</details>

<details><summary>Remove unreferenced schema from generated swagger document</summary>

When a request DTO has a property that's annotated with a `[FromBody]` attribute, the parent schema was left in the swagger document components section as an unreferenced schema. These orphaned schema will no longer be present in the generated swagger spec.

</details>

## Minor Breaking Changes ⚠️