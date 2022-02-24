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
property injection is considered an anti-pattern and constructor injection is the "standard" way to do dependency injection. that is certainly true if endpoint classes are meant to be new'ed up by the user in many places in the codebase. but the truth of the matter is endpoint classes are only instantiated by the framework itself and there's no real point in dirtying up the classes with constructors with many arguments and assigning them to private variables. it serves no real purpose and is just redundant code in this specific use-case. also there's no reason why an endpoint class would ever have public properties. i.e. nobody is ever going to create an instance of an endpoint class and set some public properties of an endpoint. that is why public properties were chosen as a "convenient" means for dependency injection. i.e. the sole purpose for the existence of a public property on an endpoint class is "for dependency injection".

it can be argued that you need to create instances of endpoint classes for unit testing. counter argument to that would be "just don't". do [integration testing with WAF](Integration-Testing.md) instead. the main reason being; sometimes the handler code may need to access things from the `HttpContext` and having to mock the HttpContext is no small task. one such example would be when your handler needs to retrieve the current user-principal's claims and cross-check something with the database. it's better not to go down the mocking rabbit-hole and [configure your mocked dependencies](https://github.com/dj-nitehawk/FastEndpoints/blob/dcc6233c3031fa253cc7138379d90ad7a1ef5b40/Test/Setup.cs#L17) via the test factory instead. 

if you believe this viewpoint is wrong, please join us on discord or open a github issue so we can discuss this further.

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