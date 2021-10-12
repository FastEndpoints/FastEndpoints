# getting started
follow the steps below to create your first endpoint that will handle an http `post` request and send a response back to the client.
## create a new project
create an empty web project with the dotnet cli using the following command or using visual studio.
```
dotnet new web -n MyWebApp
```
## install nuget package
install the latest library version using the following nuget command:
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
using FastEndpoints;

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
public class MyReqeust
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
    public MyEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
        AllowAnonymous();
    }

    protected override async Task HandleAsync(MyRequest req, CancellationToken ct)
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

that's all there's to it. you simply specify how the endpoint should be listening to incoming requests from clients in the constructor using methods such as `Verbs()`, `Routes()`, `AllowAnonymous()`, etc. then you override the `HandleAsync()` method in order to specify your handling logic. the request dto is automatically populated from the json body of your http request and passed in to the handler. when you're done processing, you call the `SendAsync()` method with a new response dto to be sent to the requesting client.

# endpoint types
there are 4 different endpoint types you can inherit from.

1. **Endpoint\<TRequest\>** - use this type if there's only a request dto.
2. **Endpoint<TRequest,TResponse>** - use this type if you have both request and response dtos.
3. **EndpointWithoutRequest** - use this type if there's no request nor response dto.
4. **EndpointWithoutRequest\<TResponse\>** - use this type if there's no request dto but there's a response dto.

it is also possible to define endpoints with `EmptyRequest` and `EmptyResponse` if needed like so:

```csharp
public class MyEndpoint : Endpoint<EmptyRequest,EmptyResponse> { }
```

you can mix-n-match as needed depending on your requirement.