---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Api Client generation using Kiota</summary>

Kiota is now the recommended way to generate API Clients. Please see the [documentation](https://fast-endpoints.com/docs/swagger-support#api-client-generation) on how
to use it. The previous methods for client generation using NSwag are still valid but may be deprecated at a future point in time.

</details>

<details><summary>Attribute based Pre/Post Processor configuration</summary>

When doing simple attribute based endpoint configuration instead of using the `Configure()` method, you can now add pre/post processors to the endpoint like so:

```csharp
[HttpPost("/test"),
 PreProcessor<PreProc>,
 PostProcessor<PostProc>]
sealed class Endpoint : Endpoint<Request, Response>
{
    public override Task HandleAsync(Request r, CancellationToken c)
    {
        ...
    }
}
```

</details>

<details><summary>Ability to specify descriptions with ACL generation</summary>

You can now specify a description/xml doc summary for individual permission items when [source generating](https://fast-endpoints.com/docs/security#source-generated-access-control-lists) them. See [the documentation](https://fast-endpoints.com/docs/security#xml-doc-summaries-for-permissions) on how to use it.

</details>

<details><summary>[HideFromDocs] attribute for removing properties from Swagger schema</summary>

```csharp
sealed class MyRequest
{
    [HideFromDocs]
    public int Internal { get; set; } //this will not appear in swagger schema

    public string Name { get; set; }
}
```

</details>

## Improvements 🚀

<details><summary>Treat validation rules with conditions attached as optional properties in Swagger spec.</summary>

If a validation rule is conditional, like in the example below, that particular DTO property will be considered optional and will not be marked as required in the Swagger Schema.

```csharp
RuleFor(x => x.Id) //this property will be a required property in the swagger spec
    .NotEmpty();   //because there's no 'When(...)' condition attached to it.

RuleFor(x => x.Age) //this will be an optional property in swagger spec because
    .NotEmpty()     //'NotEmpty()' is conditional.
    .When(SomeCondition);
```

For this to work, the rules have to be written separately as above. I.e. the `.When(...)` condition must proceed immediately after the `.NotEmpty()` or `.NotNull()` rule.

</details>

<details><summary>Support for 'UrlSegmentApiVersionReader' of 'Asp.Versioning.Http'</summary>

Only the `HeaderApiVersionReader` was previously supported. Support for doing versioning based on URL segments using the `Asp.Versioning.Http` package is now working
correctly.

</details>

<details><summary>Automatically forward endpoint attribute annotations</summary>

When using attribute annotations to configure endpoints, any custom attributes were not automatically added to endpoint metadata previously. You would've had to do
the following and use the `Configure()` method for configuration instead if you had some custom attributes you needed to use:

```csharp
Description(b => b.WithMetadata(new CustomAttribute()));
```

Now, all custom attributes are automatically added/forwarded to endpoint metadata when you configure endpoints using attribute annotations.

```csharp
[HttpGet("/"), CustomAttribute]
public class Endpoint : Endpoint<Request, Response>
```

**Note:** you still have to choose one of the strategies for endpoint configuration (attributes or configure method). Mixing both is not allowed.
</details>

<details><summary>Optimize source generators</summary>

All source generators were refactored to reduce GC pressure by reducing heap allocations. Allocations are now mostly done when there's actually a need to regenerate the
source code.

</details>

<details><summary>Micro optimization with 'Concurrent Dictionary' usage</summary>

Concurrent dictionary `GetOrAdd()` overload with lambda parameter seems to perform a bit better in .NET 8. All locations that were using the other overload was
changed to use the overload with the lambda.

</details>

## Fixes 🪲

<details><summary>'JsonNamingPolicy.SnakeCaseLower' was causing incorrect Swagger Schema properties</summary>

Snake case policy did not exist before .NET 8, so it's usage was not accounted for in the Swagger operation processor, which has now been corrected.

</details>

[//]: # (## Breaking Changes ⚠️)