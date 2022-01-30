# dependency injection
there are two ways to get access to services registered in the ioc container.

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

# automatic injection

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