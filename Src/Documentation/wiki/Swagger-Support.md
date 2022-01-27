# swagger support

swagger support is provided via the excellent `NSwag` library. your mileage may vary since nswag is presently tied closely to the mvc framework and support for .net 6 minimal api is lacking in some areas. if you find some rough edges with the swagger support in FastEndpoints, please get in touch by creating a github issue or submit a pull request if you have experience dealing with swagger.

## enable swagger

first install the `FastEndpoints.Swagger` package and add 4 lines to your app startup:

```csharp
global using FastEndpoints;
using FastEndpoints.Swagger; //add this

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddSwaggerDoc(); //add this

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.UseOpenApi(); //add this
app.UseSwaggerUi3(s => s.ConfigureDefaults()); //add this
app.Run();
```

you can then visit `/swagger` or `/swagger/v1/swagger.json` to see swagger output.

### configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddSwaggerDoc(settings =>
{
    settings.Title = "My API";
    settings.Version = "v1";
});
```

### describe endpoints
if the defaults are not satisfactory, you can clear the defaults and describe your endpoints with the `Describe()` method in configuration like so:
```csharp
public class MyEndpoint : Endpoint<MyRequest, MyResponse>
{
    public override void Configure()
    {
        Post("/admin/login");
        AllowAnonymous();
        Describe(b => b
          .Accepts<MyRequest>("application/json")
          .Produces<MyResponse>(200,"application/json")
          .ProducesProblem(500,"text/plain"));
    }
}
```

### disable auth
support for jwt bearer auth is automatically added. if you need to disable it, simply pass a `false` value to the following parameter:
```csharp
builder.Services.AddSwaggerDoc(addJWTBearerAuth: false);
```

### group endpoints by path segment
if you'd like to group your endpoints by a segment of the route url, simply specify an integer indicating which segment to use for tagging/grouping like so:
```csharp
builder.Services.AddSwaggerDoc(tagIndex: 2)
```
