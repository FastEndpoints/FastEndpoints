---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

[//]: # (## New 🎉)

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

[//]: # (## Fixes 🪲)

## Breaking Changes ⚠️

<details><summary>'SendRedirectAsync()' method signature change</summary>

The method signature has been updated to the following:

```csharp
SendRedirectAsync(string location, bool isPermanent = false, bool allowRemoteRedirects = false)
```

This would be a breaking only if you were doing any of the following:

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