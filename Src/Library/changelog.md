---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Better conditional sending of responses</summary>

All **Send.\*Async()** methods now return a T**ask\<Void\>** result. If a response needs to be sent conditionally, you can simply change the return type of the handler from **Task** to **Task\<Void\>**  and return the awaited result as shown below in order to stop further execution of endpoint handler logic:

```csharp
public override async Task<Void> HandleAsync(CancellationToken c)
{
    if (id == 0)
        return await Send.NotFoundAsync();

    if (id == 1)
        return await Send.NoContentAsync();

    return await Send.OkAsync();
}
```

If there's no async work being done in the handler, the **Task\<Void\>** can simply be returned as well:

```csharp
public override Task<Void> HandleAsync(CancellationToken c)
{
    return Send.OkAsync();
}
```

</details>

<details><summary>Specify max request body size per endpoint</summary>

Instead of globally increasing the max request body size in Kestrel, you can now set a max body size per endpoint where necessary like so:

```csharp
public override void Configure()
{
    Post("/file-upload");
    AllowFileUploads();
    MaxRequestBodySize(50 * 1024 * 1024);
}
```

</details>

<details><summary>Customize error response builder func when using 'ProblemDetails'</summary>

You can now specify a custom response builder function when doing `.UseProblemDetails()` as shown below in case you have a special requirement to use a certain shape
for one or more of your endpoints while the rest of the endpoints use the standard response.

```csharp
app.UseFastEndpoints(
       c => c.Errors.UseProblemDetails(
           p =>
           {
               p.ResponseBuilder = (failures, ctx, statusCode) =>
                                   {
                                       if (ctx.Request.Path.StartsWithSegments("/group-name"))
                                       {
                                           // return any shape you want to be serialized
                                           return new
                                           {
                                               Errors = failures
                                           };
                                       }

                                       // anything else will use the standard problem details.
                                       return new ProblemDetails(failures, ctx.Request.Path, ctx.TraceIdentifier, statusCode);
                                   };
           }))
```

</details>

<details><summary>Specify a request binder per group</summary>

It is now possible to register a particular open generic request binder such as the following:

```csharp
class MyBinder<TRequest> : RequestBinder<TRequest> where TRequest : notnull 
{ 
    public override async ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct) 
    { 
        var req = await base.BindAsync(ctx, ct); // run the default binding logic
 
        if (req is MyRequest r) 
            r.SomeValue = Guid.NewGuid().ToString(); // do whatever you like
 
        return req; 
    } 
} 
```

only for a certain group configuration, so that only endpoints of that group will have the above custom binder associated with them.

```csharp
sealed class MyGroup : Group 
{ 
    public MyGroup() 
    { 
        Configure("/my-group", ep => ep.RequestBinder(typeof(MyBinder<>))); 
    } 
} 
```

</details>

<details><summary>Position endpoint version anywhere in the route</summary>

With the built-in versioning, you could only have the endpoint version number either pre-fixed or post-fixed. You can now make the version appear anywhere in the route by using a route template. The template segment will be replaced by the actual version number instead of being prepended or appended.

```csharp
sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/sales/{_version_}/my-endpoint");
        Version(1);
    }

    ...
}
```

This version placement strategy must be enabled at startup like so:

```csharp
app.UseFastEndpoints(
       c =>
       {
           c.Versioning.RouteTemplate = "{_version_}";
       })
```

If this setting is enabled, it will take precedence over the default behavior of appending/prepending the version number to the route.

</details>

<details><summary>Support for Feature Management libraries</summary>

Endpoints can now be setup to execute a `FeatureFlag` for every Http request that comes in, which allows an endpoint to be conditionally available according to some evaluation logic.

To create a feature flag, implement the interface `IFeatureFlag` and simply return `true` from the `IsEnabledAsync()` handler method if the endpoint is to be accessible to that particular request.

```csharp
sealed class BetaTestersOnly : IFeatureFlag 
{ 
    public async Task<bool> IsEnabledAsync(IEndpoint endpoint) 
    { 
        //use whatever mechanism/library you like to determine if this endpoint is enabled for the current request. 
        if (endpoint.HttpContext.Request.Headers.TryGetValue("x-beta-tester", out _)) 
            return true; // return true to enable 
 
        //this is optional. if you don't send anything, a 404 is sent automatically. 
        await endpoint.HttpContext.Response.SendErrorsAsync([new("featureDisabled", "You are not a beta tester!")]); 
 
        return false; // return false to disable 
    } 
}
```

Attach it to the endpoint like so:

```csharp
sealed class BetaEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("beta");
        FeatureFlag<BetaTestersOnly>();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await Send.OkAsync("this is the beta!");
    }
}
```

</details>

## Improvements 🚀

<details><summary>Recursive validation of  Data Annotation Attributes</summary>

Until now, only the top level properties of a request DTO was being validated when using Data Annotation Attributes. This release adds support for recursively validating the whole object graph and generating errors for each that fails validation.

</details>

<details><summary>SSE response standard compliance</summary>

The SSE response implementation has been enhanced by making the `Id` property in `StreamItem` optional, adding an optional `Retry` property for client-side reconnection control, as well as introducing an extra `StreamItem` constructor overload for more flexibility. Additionally, the `X-Accel-Buffering: no` response header is now automatically sent to improve compatibility with reverse proxies like NGINX, ensuring streamed data is delivered without buffering. You can now do the following when doing multi-type data responses:

```csharp
yield return new StreamItem("my-event", myData, 3000);
```

</details>

<details><summary>Respect app shutdown when using SSE</summary>

The SSE implementation now passes the `ApplicationStopping` cancellation token to your `IAsyncEnumerable` method. This means that streaming is cancelled at least when the application host is shutting down, and also when a user provided `CancellationToken` (if provided) triggers it.

```csharp
public override async Task HandleAsync(CancellationToken ct)
{
    await Send.EventStreamAsync(GetMultiDataStream(ct), ct);

    async IAsyncEnumerable<StreamItem> GetMultiDataStream([EnumeratorCancellation] CancellationToken ct)
    {
        // Here ct is now your user provided CancellationToken combined with the ApplicationStopping CancellationToken.
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);

            yield return new StreamItem(Guid.NewGuid(), "your-event-type", 42);
        }
    }
}
```

</details>

<details><summary>Async variation of Global Response Modifier</summary>

TODO: describe it

</details>

## Fixes 🪲

<details><summary>Incorrect enum value for JWT security algorithm was used</summary>

The wrong variant (`SecurityAlgorithms.HmacSha256Signature`) was being used for creating symmetric JWTs by default.
The default value has been changed to `SecurityAlgorithms.HmacSha256`. It's recommended to invalidate and regenerate new tokens if you've been using the default.

If for some reason, you'd like to keep using `SecurityAlgorithms.HmacSha256Signature`, you can set it yourself like so:

```csharp
var token = JwtBearer.CreateToken(
    o =>
    {
        o.SigningKey = ...;
        o.SigningAlgorithm = SecurityAlgorithms.HmacSha256Signature;
    });
```

</details>


<details><summary>Integration testing extensions ignoring custom header names</summary>

The testing httpclient extensions were ignoring user supplied custom header names such as the following:

```csharp
[FromHeader("x-something")]
```

during the constructing of the http request message. It was instead using the DTO property name completely dismissing the custom header names.

</details>

<details><summary>Integration test extensions causing 404 if grouped endpoint configured with empty string</summary>

The test helper methods were constructing the url/route of the endpoint being tested incorrectly if that endpoint belonged to a group and was configured with an empty route like so:

```csharp
sealed class MyGroup : Group 
{ 
    public MyGroup() 
    { 
        Configure("my-group", ep => ep.AllowAnonymous()); 
    } 
} 
 
sealed class Request 
{ 
    [QueryParam] 
    public string Id { get; set; } 
} 
 
sealed class RootEndpoint : Endpoint<Request, string> 
{ 
    public override void Configure() 
    { 
        Get(string.Empty); 
        Group<MyGroup>(); 
    } 
 
    ...
}
```

</details>

<details><summary>Swagger generation failing when DTO inherits a virtual base property</summary>

When a base class has a virtual property that a derived class was overriding as shown below, Swagger generator was throwing an exception due an internal dictionary key duplication.

```csharp
public abstract class BaseDto
{
    public virtual string Name { get; set; }
}

sealed class DerivedClass : BaseDto
{
    public override string Name { get; set; }
}
```

</details>

## Breaking Changes ⚠️