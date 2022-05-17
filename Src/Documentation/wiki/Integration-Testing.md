# route-less integration testing

the recommended approach to test your endpoints is to perform integration testing using the `WebApplicationFactory`. 
this library offers a set of extensions to the `HttpClient` to make testing more convenient in a strongly-typed and route-less manner. 
i.e. you don't need to specify the route urls when testing endpoints. follow the simple steps below to start WAF testing your endpoints:

> [!NOTE]
> this document is still a work-in-progress.
> please check back soon...

> you can have a look at the [test project here](https://github.com/dj-nitehawk/FastEndpoints/tree/v4.1.0/Test) in the meantime to get an idea.

# unit testing endpoints
if you don't mind paying the price of extra work needed for more granular testing with unit tests, you may use the `Factory.Create<TEndpoint>()` method to get an instance of your endpoint which is suitable for unit testing.

```csharp
[TestMethod]
public async Task AdminLoginSuccess()
{
    //arrange
    var fakeConfig = A.Fake<IConfiguration>();
    A.CallTo(() => fakeConfig["TokenKey"]).Returns("0000000000000000");

    var ep = Factory.Create<AdminLogin>(
        A.Fake<ILogger<AdminLogin>>(), //mock dependencies for injecting to the constructor
        A.Fake<IEmailService>(),
        fakeConfig);

    var req = new AdminLoginRequest
    {
        UserName = "admin",
        Password = "pass"
    };

    //act
    await ep.HandleAsync(req, default);
    var rsp = ep.Response;

    //assert
    Assert.IsNotNull(rsp);
    Assert.IsFalse(ep.ValidationFailed);
    Assert.IsTrue(rsp.Permissions.Contains("Inventory_Delete_Item"));
}
```

use the `Factory.Create()` method by passing it the mocked dependencies which are needed by the endpoint constructor, if there's any. it has multiple overloads that enables you to instantiate endpoints with or without constructor arguments.

then simply execute the handler by passing in a request dto and a default cancellation token.

finally do your assertions on the `Response` property of the endpoint instance.

### handler method which returns the response dto
if you prefer to return the dto object from your handler, you can implement the `ExecuteAsync()` method instead of `HandleAsync()` like so:
```csharp
public class AdminLogin : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/admin/login");
        AllowAnonymous();
    }

    public override Task<Response> ExecuteAsync(Request req, CancellationToken ct)
    {
        return Task.FromResult(
            new Response
            {
                JWTToken = "xxx",
                ExpiresOn = "yyy"
            });
    }
}
```

by doing the above, you can simply access the response dto like below instead of through the `Response` property of the endpoint when unit testing.

```csharp
var res = await ep.ExecuteAsync(req, default);
```