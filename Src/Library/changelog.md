---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Support for .Net 8.0</summary>

The project is now developed and built using .NET 8.0 while supporting .NET 6 & 7 as well. You can upgrade your existing projects simply by targeting .NET 8

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
     </PropertyGroup>
</Project>
```

The .NET 8 [Request Delegate Generator](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/aot/request-delegate-generator/rdg?view=aspnetcore-8.0) is not yet
compatible with FastEndpoints as FE has it's own endpoint mapping and model binding system which will require a complete rewrite as a Source Generator to properly
support Native AOT. We're currently investigating ways to achieve that but cannot give a timeframe on completion as it's a massive undertaking. You can see
our [internal discussion](https://discord.com/channels/933662816458645504/1174563570013442098) about this matter on discord.

</details>

<details><summary>Simpler way to register Pre/Post Processors with DI support</summary>

Processors can now be configured just by specifying the type of the processor without the need for instantiating them yourself.

```cs
public class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        ...
        PreProcessor<PreProcessorOne>();
        PreProcessor<PreProcessorTwo>();
    }
}
```

While the old **PreProcessors(...)** method continues to work, the new method automatically resolves any constructor injected dependencies without you having to manually
register the processors in DI.
</details>

<details><summary>Exception handling capability for Post-Processors</summary>

Post-Processors can now handle uncaught exceptions as an alternative to an exception handling middleware.
Please see the [documentation page](https://fast-endpoints.com/docs/pre-post-processors#handling-unhandled-exceptions-with-post-processors) for details.

</details>

<details><summary>Shared state support for global Pre/Post Processors</summary>

Global Pre/Post Processors now have [shared state](https://fast-endpoints.com/docs/pre-post-processors#sharing-state) support with the following two newly added abstract
types:

- GlobalPreProcessor\<TState\>
- GlobalPostProcessor\<TState\>

</details>

<details><summary>Ability to hide the error reason in the JSON response when using the default exception handler middleware</Summary>

The actual error reason can now be hidden from the client by configuring
the [exception handler middleware](https://fast-endpoints.com/docs/exception-handler#unhandled-exception-handler) like so:

```cs
app.UseDefaultExceptionHandler(useGenericReason: true);
```

</details>

[//]: # (## Improvements 🚀)

## Fixes 🪲

<details><summary>Auto binding collections of form files fails after first request</summary>

An object disposed error was being thrown in subsequent requests for file collection submissions due to a flaw in the model binding logic, which has now been corrected.

</details>

<details><summary>Incorrect validation error field names when nested request DTO property has [FromBody] attribute</summary>

JSON error responses didn't correctly render the deeply nested property chain/paths when the following conditions were met:

- Request DTO has a property annotated with `[FromBody]` attribute
- The bound property is a complex object graph
- Some validation errors occur for deeply nested items

This has been fixed to correctly render the property chain of the actual item that caused the validation failure.

More info [here](https://discord.com/channels/933662816458645504/1168177198415482972).

</details>

<details><summary>Test assertions couldn't be done on ProblemDetails DTO</summary>

The `ProblemDetails` DTO properties had private setter properties preventing STJ from being able to deserialize the JSON which has now been corrected.

</details>

## Minor Breaking Changes ⚠️

<details><summary>Pre/Post Processor interface changes</summary>

Due to the Processor related new features introduced in this release, the `*ProcessAsync(...)` method signatures had to be changed. The previous arguments are still
available but they have been grouped/pushed into a processor context object. The new method signatures look like the following. Migrating your existing pre/post
processors shouldn't take more than a few minutes.

**PreProcessor Signature:**

```cs
sealed class MyProcessor : IPreProcessor<MyRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<MyRequest> ctx, CancellationToken c)
    {
        ...
    }
}
```

**PostProcessor Signature:**

```cs
sealed class MyProcessor : IPostProcessor<MyRequest, MyResponse>
{
    public Task PostProcessAsync(IPostProcessorContext<MyRequest, MyResponse> ctx, CancellationToken c)
    {
        ...
    }
}
```

</details>

<details><summary>AddJWTBearerAuth() default claim type mapping behavior change</summary>

The `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` static dictionary is presently used by ASP.NET for mapping claim types for inbound claim type mapping. In most
cases people use `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` to not have long claim types such
as `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` as the claim type for the `sub` claim for example.

The default behavior has now been changed to make the claim types not use the SOAP type identifiers like above. If for some reason you'd like to revert to the old
behavior, it can be achieved like so:

```csharp
.AddJWTBearerAuth("jwt_signing_key", o =>
{
    o.MapInboundClaims = true;
});
```

> If using the new default behavior, it is **highly** recommended to invalidate any previously issued JWTs by resetting your JWT signing keys or any other means
> neccessary to avoid any potential issues with claim types not matching. This obviously means your clients/users will have re-login (obtain new JWTs). If that's not an
> option, simply set `.MapInboundClaims = true;` as mentioned above to use the previous behavior.

See #526 for more info.

</details>