# swagger support

you can choose between `NSwag` or `Swashbuckle` based swagger support. however, do note that your mileage may vary since those libraries are presently tied closely to the mvc framework and support for .net 6 minimal api is lacking in some areas. if you find some rough edges with the swagger support in FastEndpoints, please get in touch by creating a github issue or submit a pull request if you have experience dealing with swagger.

# [NSwag](#tab/nswag)

## enable nswag

first install the `FastEndpoints.NSwag` package and add 4 lines to your app startup:

```csharp
global using FastEndpoints;
using FastEndpoints.NSwag; //add this

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddNSwag(); //add this

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
builder.Services.AddNSwag(settings =>
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
builder.Services.AddNSwag(addJWTBearerAuth: false);
```

### json serializer options
swagger serialization options can be set with the following parameter:
```csharp
builder.Services.AddNSwag(serializerOptions:
    o => o.JsonSerializerOptions.PropertyNamingPolicy = null);
```

### group endpoints by path segment
if you'd like to group your endpoints by a segment of the route url, simply specify an integer indicating which segment to use for tagging/grouping like so:
```csharp
builder.Services.AddNSwag(tagIndex: 2)
```

# [Swashbuckle](#tab/swashbuckle)

## enable swashbuckle

first install the `FastEndpoints.Swashbuckle` package and add 4 lines to your app startup:

```csharp
global using FastEndpoints;
using FastEndpoints.Swagger; //add this

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddSwashbuckle(); //add this

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.UseSwagger(); //add this
app.UseSwaggerUI(o => o.ConfigureDefaults()); //add this
app.Run();
```

you can then visit `/swagger` or `/swagger/v1/swagger.json` to see swagger output.

### configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddSwashbuckle(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    options.CustomSchemaIds(x => x.Name);
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
builder.Services.AddSwashbuckle(addJWTBearerAuth: false);
```

### json serializer options
swagger serialization options can be set with the following parameter:
```csharp
builder.Services.AddSwashbuckle(serializerOptions:
    o => o.JsonSerializerOptions.PropertyNamingPolicy = null);
```

### group endpoints by path segment
if you'd like to group your endpoints by a segment of the route url, simply specify an integer indicating which segment to use for tagging/grouping like so:
```csharp
builder.Services.AddSwashbuckle(tagIndex: 2)
```