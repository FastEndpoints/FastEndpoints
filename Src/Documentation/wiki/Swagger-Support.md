# swagger support

you can choose between `NSwag` or `Swashbuckle` based swagger support. however, do note that your mileage may vary since those libraries are presently tied closely to the mvc framework and support for .net 6 minimal api is lacking in some areas. if you find some rough edges with the swagger support in FastEndpoints, please get in touch by creating a github issue or submit a pull request if you have experience dealing with swagger.

# [NSwag](#tab/nswag)

## nswag library

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
app.UseSwaggerUi3(); //add this
app.Run();
```

you can then visit `/swagger` or `/swagger/v1/swagger.json` to see swagger output.

### swagger configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddNSwag(settings =>
{
    settings.Title = "My API";
    settings.Version = "v1";
    settings.SchemaNameGenerator = new MySchemaNameGenerator();
    settings.AddOperationFilter(ctx =>
    {
        ctx.OperationDescription.Operation.Tags.Add(
            ctx.OperationDescription.Path.Split('/')[1]);
        return true;
    });
});
```

### describe your endpoints
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

### disable swagger auth
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

# [Swashbuckle](#tab/swashbuckle)

## swashbuckle library

first install the `FastEndpoints.Swagger` package and add 4 lines to your app startup:

```csharp
global using FastEndpoints;
using FastEndpoints.Swagger; //add this

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddSwagger(); //add this

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.UseSwagger(); //add this
app.UseSwaggerUI(); //add this
app.Run();
```

you can then visit `/swagger` or `/swagger/v1/swagger.json` to see swagger output.

### swagger configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddSwagger(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    options.CustomSchemaIds(x => x.Name);
    options.TagActionsBy(x => new[] { x.RelativePath?.Split('/')[1] });
});
```

### describe your endpoints
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

### disable swagger auth
support for jwt bearer auth is automatically added. if you need to disable it, simply pass a `false` value to the following parameter:
```csharp
builder.Services.AddSwagger(addJWTBearerAuth: false);
```

### json serializer options
swagger serialization options can be set with the following parameter:
```csharp
builder.Services.AddSwagger(serializerOptions:
    o => o.JsonSerializerOptions.PropertyNamingPolicy = null);
```