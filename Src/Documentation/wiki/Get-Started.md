# getting started
follow the steps below to create your first endpoint that will handle an http `post` request and send a response back to the client.
## create a new project
create an empty web project with the dotnet cli using the following command or using visual studio.
```
dotnet new web -n MyWebApp
```
## install nuget package
install the latest library version using the following cli command:
```
dotnet add package FastEndpoints
```
or with nuget package manager:
```
Install-Package FastEndpoints
```
## prepare startup
replace the contents of `Program.cs` file with the following:
```csharp
global using FastEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```
## add a request dto
create a file called `MyRequest.cs` and add the following:
```csharp
public class MyRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}
```
## add a response dto
create a file called `MyResponse.cs` and add the following:
```csharp
public class MyResponse
{
    public string FullName { get; set; }
    public bool IsOver18 { get; set; }
}
```

## add an endpoint definition
create a file called `MyEndpoint.cs` and add the following:
```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        var response = new MyResponse()
        {
            FullName = req.FirstName + " " + req.LastName,
            IsOver18 = req.Age > 18
        };

        await SendAsync(response);
    }
}
```
now run your web app and send a `POST` request to the `/api/user/create` endpoint using a REST client such as postman with the following request body:
```
{
    "FirstName": "marlon",
    "LastName": "brando",
    "Age": 40
}
```
you should then get a response back such as this:
```
{
    "FullName": "marlon brando",
    "IsOver18": true
}
```

that's all there's to it. you simply configure how the endpoint should be listening to incoming requests from clients in the `Configure()` section calling methods such as `Verbs()`, `Routes()`, `AllowAnonymous()`, etc. then you override the `HandleAsync()` method in order to specify your handling logic. the request dto is automatically populated from the json body of your http request and passed in to the handler. when you're done processing, you call the `SendAsync()` method with a new response dto to be sent to the requesting client. 

# endpoint types
there are 4 different endpoint base types you can inherit from.

1. **Endpoint\<TRequest\>** - use this type if there's only a request dto. you can however send any object to the client that can be serialized as a response with this generic overload.
2. **Endpoint<TRequest,TResponse>** - use this type if you have both request and response dtos. the benefit of this generic overload is that you get strongly-typed access to properties of the dto when doing integration testing and validations.
3. **EndpointWithoutRequest** - use this type if there's no request nor response dto. you can send any serializable object as a response here also.
4. **EndpointWithoutRequest\<TResponse\>** - use this type if there's no request dto but there is a response dto.

it is also possible to define endpoints with `EmptyRequest` and `EmptyResponse` if needed like so:

```csharp
public class MyEndpoint : Endpoint<EmptyRequest,EmptyResponse> { }
```

# sending responses
there are multiple response **[sending methods](Misc-Conveniences.md#send-methods)** you can use. it is also possible to simply populate the `Response` [property of the endpoint](Misc-Conveniences.md#response-tresponse) and get a 200 ok response with the value of the `Response` property serialized in the body automatically. for ex:

**response dto:**
```csharp
public class MyResponse
{
    public string FullName { get; set; }
    public int Age { get; set; }
}
```
**endpoint definition:**
```csharp
public class MyEndpoint : EndpointWithoutRequest<MyResponse>
{
    public override void Configure()
    {
        Get("/api/person");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var person = await dbContext.GetFirstPersonAsync();

        Response.FullName = person.FullName;
        Response.Age = person.Age;
    }
}
```
assigning a new instance to the `Response` property also has the same effect:
```csharp
public override Task HandleAsync(CancellationToken ct)
{
    Response = new()
    {
        FullName = "john doe",
        Age = 124
    };
    return Task.CompletedTask;
}
```

# configuring endpoints using attributes
instead of overriding the `Configure()` method, endpoint classes can be annotated with `[HttpGet(...)]`,`[AllowAnonymous]`, and `[Authorize(...)]` attributes. advanced usage however does require overriding `Configure()`. you can only use one of these strategies for configuring endpoints. an exception will be thrown if you use both or none at all.

```csharp
[HttpPost("/my-endpoint")]
[Authorize(Roles = "Admin,Manager")]
public class UpdateAddress : Endpoint<MyRequest, MyResponse>
{
    public override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        await SendAsync(new MyResponse { });
    }
}
```