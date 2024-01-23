---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Attribute driven response headers</summary>

Please see the [documentation](https://fast-endpoints.com/docs/misc-conveniences#attribute-driven-response-headers) for more information.

</details>

<details><summary>Support for generic commands and command handlers</summary>

Please see the [documentation](https://fast-endpoints.com/docs/command-bus#generic-commands-handlers) for more information.

</details>

<details><summary>Allow a Post-Processor to act as the sole mechanism for sending responses</summary>

As shown in [this example](https://gist.github.com/dj-nitehawk/6e23842dcb7640b165fd80ba57967540), a post-processor can now be made the sole orchestrator of sending the 
appropriate response such as in the case with the "Results Pattern".

</details>

## Improvements 🚀

<details><summary>Auto resolving of Mappers in unit tests</summary>

Previously it was necessary for the user to instantiate and set the mapper on endpoints when unit testing endpoints classes. It is no longer necessary to do so
unless you want to. Existing code doesn't need to change as the `Mapper` property is still publicly settable.

</details>

<details><summary>Respect default values of constructor arguments when model binding</summary>

The default request binder will now use the default values from the constructor arguments of the DTO when instantiating the DTO before model binding starts. For
example, the `SomeOtherParam` property will have a value of `10` if no other binding sources provides a value for it.

```csharp
record MyRequest(string SomeParam,
                 int SomeOtherParam = 10);
```

</details>

<details><summary>Warn user about illegal request DTO types</summary>

FastEndpoints only supports model binding with DTOs that have publicly accessible properties. The following is not supported:

```csharp
sealed class MyEndpoint : Endpoint<Guid>
```

A more detailed `NotSupportedException` is now being thrown to make it easy track down the offending endpoint.

</details>

<details><summary>Property naming policy was not applied to route parameters when generating Swagger spec</summary>

If you had a request DTO like this:

```csharp
sealed class MyRequest
{
    public long SomeId { get; set; }
}
```

And a route like this:

```csharp
public override void Configure()
{
    Get("/something/{someID}");
}
```

Where the case of the parameter is different, and also had a property naming policy applied like this:

```csharp
app.UseFastEndpoints(c => c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower)
```

Previously the Swagger spec generated would have a mismatched operation path parameter `{someID}` and a Swagger request parameter `some-id`.

Now the Swagger path parameter is correctly rendered to match with the exact value/case as the request parameter.

</details>

## Fixes 🪲

<details><summary>Type discovery source generator creating duplicates for partial classes</summary>

The type discovery source generator will now correctly detect partial classes of targets and only create a single entry. #574

</details>

<details><summary>Correct handling of Swagger request param examples</summary>

Examples for request parameters were previously rendered as strings instead of the respective primitives or json objects.

Given the DTO model (with examples as xml tags):

```csharp
sealed class MyRequest
{
    /// <example>
    /// 10
    /// </example>
    public int SomeNumber { get; set; }

    /// <example>
    /// ["blah1","blah2"]
    /// </example>
    public string[] SomeList { get; set; }

    /// <example>
    /// { id : 1000, name : "john" }
    /// </example>
    public Nested SomeClass { get; set; }

    public sealed class Nested
    {
        public int Id { get; set; }
        public Guid GuidId { get; set; }
        public string Name { get; set; }
    }
}
```

Will now be correctly rendered as follows:

```json
"parameters": [
    {
        "name": "someNumber",
        "example": 10
    },
    {
        "name": "someList",        
        "example": [
            "blah1",
            "blah2"
        ]
    },
    {
        "name": "someClass",        
        "example": {
            "id": 1000,
            "name": "john"
        }
    }
]
```

</details>

## Breaking Changes ⚠️

<details><summary>'SendRedirectAsync()' method signature change</summary>

The method signature has been updated to the following:

```csharp
SendRedirectAsync(string location, bool isPermanent = false, bool allowRemoteRedirects = false)
```

This would be a breaking change only if you were doing any of the following:

- Redirecting to a remote url instead of a local url. In which case simply set `allowRemoteRedirects` to `true`. otherwise the new behavior will throw an exception.
  this change was done to prevent [open redirect attacks](https://learn.microsoft.com/en-us/aspnet/mvc/overview/security/preventing-open-redirection-attacks) by default.

- A cancellation token was passed in to the method. The new method does not support cancellation due to the underlying `Results.Redirect(...)` methods do not support
  cancellation.

</details>

<details><summary>Minor behavior change for exception handling with 'Post Processors'</summary>

Previously when an exception is [handled by a post-processor](https://fast-endpoints.com/docs/pre-post-processors#handling-unhandled-exceptions-with-post-processors)
the captured exception would only be thrown out to the middleware pipeline in case the post-processor hasn't already written to the response stream. Detecting this
reliably has proven to be difficult and now your post-processor must explicitly call the following method if it's handling the exception itself and don't need the
exception to be thrown out to the pipeline.

```csharp
public class ExceptionProcessor : IPostProcessor<Request, Response>
{
    public async Task PostProcessAsync(IPostProcessorContext<Request, Response> ctx, ...)
    {
        ctx.MarkExceptionAsHandled();
        //do your exception handling after this call
    }
}
```

</details>

<details><summary>Rename 'UseAntiforgery()' method</summary>

The `builder.Services.UseAntiForgery()` extension method has been renamed to `.UseAntiforgeryFE()` in order to avoid confusion.

</details>