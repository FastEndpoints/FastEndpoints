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
