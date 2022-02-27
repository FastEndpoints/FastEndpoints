# dependency injection
there are three different ways to get access to services registered in the ioc container.

consider the following service registration.

**the service**
```csharp
public interface IHelloWorldService
{
    string SayHello();
}

public class HelloWorldService : IHelloWorldService
{
    public string SayHello() => "hello world!";
}
```

**ioc registration**
```
builder.Services.AddScoped<IHelloWorldService, HelloWorldService>();
```

# property injection

services can be automatically property injected by simply adding properties to the endpoint like so:

```csharp
public class MyEndpoint : EndpointWithoutRequest
{
    public IHelloWorldService HelloService { get; set; }

    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/hello-world");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendAsync(HelloService.SayHello());
    }
}
```

# constructor injection
constructor injection is also supported. just make sure not to assign the injected dependencies to public properties if using together with property injection.
```csharp
public class MyEndpoint : EndpointWithoutRequest
{
    private IHelloWorldService _helloService;

    public MyEndpoint(IHelloWorldService helloScv)
    {
        _helloService = helloScv;
    }

    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/hello-world");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendAsync(_helloService.SayHello());
    }
}
```

# manual resolving

services can be resolved manually like so:
```csharp
public override async Task HandleAsync(CancellationToken ct)
{
    IHelloWorldService? helloSvc = TryResolve<IHelloWorldService>();

    if (helloSvc is null)
        ThrowError("service not resolved!");

    var logger = Resolve<ILogger<MyEndpoint>>();

    logger.LogInformation("hello service is resolved...");

    await SendAsync(helloSvc.SayHello());
}
```
**TryResolve()** - this method will try to resolve the given service. returns null if not resolved.

**Resolve()** - this method will throw an exception if the requested service cannot be resolved.

# pre-resolved services
the following services are pre-resolved and available for every endpoint handler with the following properties:
```
property: Config
service : IConfiguration

property: Env
service : IWebHostEnvironment

property: Logger
service : ILogger
```

they can be used in the endpoint handlers like so:
```csharp
public override async Task HandleAsync(CancellationToken ct)
{
    Logger.LogInformation("this is a log message");
    var isProduction = Env.IsProduction();
    var smtpServer = Config["SMTP:HostName"];
    ...
}
```

# dependency resolving for validators
by default, validators are registered in the DI container as singletons for [performance reasons](Benchmarks.md). both the above-mentioned `Resolve()` and `TryResolve()` methods are available for validators to get access to the dependencies it needs. you should also take care not to maintain state in the validator due to it being singleton scope.

if for some reason you don't mind paying the performance penalty and would like to either maintain state in the validator or would like to do constructor injection, you may do so by instructing the endpoint to register the validator as a scoped dependency like so:

```csharp
public override void Configure()
{
    Get("/hello-world");
    ScopedValidator();
}
```
once you enable the validator to be registered as a `Scoped` dependency, you can use constructor injection on the validator like so:
```csharp
public class MyValidator : Validator<MyRequest>
{
    public MyValidator(IConfiguration config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));
    }
}
```