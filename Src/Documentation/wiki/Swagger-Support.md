# enabling swagger

in order to enable swaggergen/ui for your project, first install the `FastEndpoints.Swagger` package and add 4 lines to your app startup:

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

## swagger configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddSwagger(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    options.CustomSchemaIds(x => x.Name);
    options.TagActionsBy(x => new[] { x.RelativePath?.Split('/')[1] });
});
```

## disable swagger auth
support for jwt bearer auth is automatically added. if you need to disable it, simply pass a `false` value to the following parameter:
```csharp
builder.Services.AddSwagger(addJWTBearerAuth: false);
```

## json serializer options
swagger serialization options can be set with the following parameter:
```csharp
builder.Services.AddSwagger(serializerOptions:
    o => o.JsonSerializerOptions.PropertyNamingPolicy = null);
```

# limitations
swagger/swashbuckle is presently tied closely to the mvc framework and support for .net 6 minimal api is lacking some features. hence, your mileage may vary. 
if you find some rough edges with the swagger support in FastEndponts, please get in touch by creating a github issue or better yet, submit a pull request if you have experience dealing with swagger.