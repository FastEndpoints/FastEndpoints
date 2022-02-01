# pre/post processors

rather than writing a common piece of logic repeatedly that must be executed either before or after each request to your system, you can write it as a processor and attach it to endpoints that need them. there are two types of processors. `pre-processors` and `post-processors`.

# pre-processors

let's say for example that you'd like to log every request before being executed by your endpoint handlers. you can simply write a pre-processor like below by implementing the interface **IPreProcessor\<TRequest\>**:

```csharp
public class MyRequestLogger<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<TRequest>>();

        logger.LogInformation($"request:{req?.GetType().FullName} path: {ctx.Request.Path}");

        return Task.CompletedTask;
    }
}
```
and then attach it to the endpoints you need like so:
```csharp
public class CreateOrderEndpoint : Endpoint<CreateOrderRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/sales/orders/create");
        PreProcessors(new MyRequestLogger<CreateOrderRequest>());
    }
}
```

you can even write a request dto specific processor like so:

```csharp
public class SalesRequestLogger : IPreProcessor<CreateSaleRequest>
{
    public Task PreProcessAsync(CreateSaleRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<CreateSaleRequest>>();

        logger.LogInformation($"sale value:{req.SaleValue}");

        return Task.CompletedTask;
    }
}
```

## short-circuiting execution
it is possible to end processing the request by returning a response from within a pre-processor like so:
```csharp
public class SecurityProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var tenantID = ctx.Request.Headers["tenant-id"].FirstOrDefault();

        if (tenantID == null)
        {
            failures.Add(new("MissingHeaders", "The [tenant-id] header needs to be set!"));
            return ctx.Response.SendErrorsAsync(failures); //sending response here
        }

        if (tenantID != "qwerty")
            return ctx.Response.SendForbiddenAsync(); //sending response here

        return Task.CompletedTask;
    }
}
```
all the [Send* methods](Misc-Conveniences.md#send-methods) supported by endpoint handlers are available. the send methods are accessed from the `ctx.Response` property as shown above. when a response is sent from a pre-processor, the handler method is not executed. however, if there are multiple pre-processors configured, they will be executed. if another pre-processor also wants to send a response, they must check if it's possible to do so by checking the property `ctx.Response.HasStarted` to see if a previously executed pre-processor has already sent a response to the client.

# post-processors

post-processors are executed after your endpoint handler has completed it's work. they can be created similarly by implementing the interface **IPostProcessor<TRequest, TResponse>**:

```csharp
public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
{
    public Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<TResponse>>();

        if (res is CreateSaleResponse response)
        {
            logger.LogWarning($"sale complete: {response.OrderID}");
        }

        return Task.CompletedTask;
    }
}
```
and then attach it to endpoints like so:
```csharp
public class CreateOrderEndpoint : Endpoint<CreateSaleRequest, CreateSaleResponse>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/sales/orders/create");
        PostProcessors(new MyResponseLogger<CreateSaleRequest, CreateSaleResponse>());
    }
}
```

# multiple processors
you can attach multiple processors with both `PreProcessors()` and `PostProcessors()` methods. the processors are executed in the order they are supplied to the methods.

# global processors/ filters
the recommended approach for global filters/ processors is to write that logic as a middleware and register it in the asp.net pipeline like so:
```csharp
app.UseMiddleware<MyMiddleware>()
```

as an alternative to that, you can write a base endpoint like below which includes a processor and derive your endpoint classes from that.
```csharp
public abstract class PublicEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
where TRequest : class, new()
where TResponse : notnull, new()
{
    public override void Configure()
    {
        PreProcessors(new MyRequestLogger<TRequest>());
        AllowAnonymous();
    }
}

public class MyEndpoint : PublicEndpoint<EmptyRequest, EmptyResponse>
{
    public override void Configure()
    {
        Get("test/global-preprocessor");
        base.Configure();
    }

    public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        return SendOkAsync();
    }
}
```
this approach is also helpful if you'd like to configure several endpoints with the same base configuration.