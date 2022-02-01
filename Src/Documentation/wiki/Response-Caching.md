# response caching
in order to get response caching working, you need to enable the response caching middleware and define how responses are cached using the `ResponseCache()` method in the endpoint configuration. this method supports all arguments of the `[ResponseCache]` attribute you'd typically use with mvc except for the `CacheProfileName` argument as cache profiles are not supported. [see this document](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-6.0) for an intro to response caching in asp.net middleware.

**startup**
```csharp
global using FastEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddResponseCaching(); //add this

var app = builder.Build();
app.UseAuthorization();
app.UseResponseCaching(); //add this
app.UseFastEndpoints();
app.Run();
```

**endpoint**
```csharp
public class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/cached-ticks");
        ResponseCache(60); //cache for 60 seconds
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendAsync(new
        {
            Message = "this response is cached"
            Ticks = DateTime.UtcNow.Ticks
        });
    }
}
```