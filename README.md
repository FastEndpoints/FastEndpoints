# EZEndpoints

An easy to use Web API framework (which encourages CQRS and Vertical Slice Architecture) built on top of .Net 6 Endpoints. It is a great alternative to the new minimal APIs that require manual endpoint registration/mapping.


Current State: **NOT PRODUCTION READY!!!**


## Try it out...
there is still no nuget package published. so you'd have to clone this git repo and reference the `/Src/EZEndpoints.cs` as a project reference from a .net core 6 project. or you can play around with the sample project in `/Web/Web.csproj`.

# Code Sample:

### Program.cs
```csharp
using EZEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Services.AddEZEndpoints();
builder.Services.AddAuthenticationJWTBearer("Key");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseEZEndpoints();
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
public class MyEndpoint : Endpoint<MyRequest, MyValidator>
{
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

    protected override Task ExecuteAsync(MyRequest req, Context<MyRequest> ctx)
    {
        if (req.Price < 100)
            ctx.AddError(r => r.Price, "Price is too low!");

        ctx.AddError("This is a general error!");

        //this will send a 400 error response with a json object containing error details.
        ctx.ThrowIfAnyErrors();

        Logger.LogInformation("this is your first endpoint!");

        var dbService = Resolve<IDataService>();

        var res = new MyResponse
        {
            Message = $"the route parameter value is: {req.Id}",
            Name = req.Name,
            Price = req.Price
        };

        return ctx.SendAsync(res);
    }
}
```

that's it. all of your `Endpoint` definitions are automatically discovered on app startup and routes are automatically mapped.

# Stay tuned...

if the above api looks interesting to you, watch this repo for updates. you are welcome to submit PRs or suggest features/ report bugs using github issues.
