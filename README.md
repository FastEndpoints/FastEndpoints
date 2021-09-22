# ApiExpress

An easy to use Web API framework (which encourages CQRS and Vertical Slice Architecture) built as an extension to the Asp.Net pipeline. It is a great alternative to the new minimal APIs that require manual endpoint mapping.

Current State: **NOT PRODUCTION READY!!!**

## Try it out...
there is still no nuget package published. so you'd have to clone this git repo and reference the `/Src/ApiExpress.cs` as a project reference from a .net core 6 project. or you can play around with the sample project in `/Web/Web.csproj`.

# Code Sample:

### Program.cs
```csharp
using ApiExpress;

var builder = WebApplication.CreateBuilder();
builder.Services.AddApiExpress();
builder.Services.AddAuthenticationJWTBearer("Key");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseApiExpress();
app.Run();
```

### Request DTO
```csharp
public class MyRequest : IRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Price { get; set; }
}
```

### Response DTO
```csharp
public class Response : IResponse
{
    public string? Name { get; internal set; }
    public int Price { get; set; }
    public string? Message { get; set; }
}
```

### Endpoint Definition
```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public ILogger<MyEndpoint>? Logger { get; set; } //automatically injected from services

    public MyEndpoint()
    {
        Routes("/api/test/{id}");
        Verbs(Http.POST, Http.PATCH);

        Roles("Admin", "Manager");
        Policies("TopManagement", "Auditors");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Retrieve_Item,
            Allow.Inventory_Update_Item);
    }

    protected override Task ExecuteAsync(MyRequest req, CancellationToken cancellation)
    {
        if (req.Price < 100)
            AddError(r => r.Price, "Price is too low!");

        AddError("This is a general error!");

        ThrowIfAnyErrors(); //this will send a 400 error response with a json object containing error details.

        Logger.LogInformation("this is your first endpoint!");

        var res = new MyResponse
        {
            Message = $"the route parameter value is: {req.Id}",
            Name = req.Name,
            Price = req.Price
        };

        return SendAsync(res);
    }
}
```

that's it. all of your `Endpoint` definitions are automatically discovered on app startup and routes automatically mapped.

# Stay tuned...

if the above api looks interesting to you, watch this repo for updates. you are welcome to submit PRs or suggest features/ report bugs using github issues.
