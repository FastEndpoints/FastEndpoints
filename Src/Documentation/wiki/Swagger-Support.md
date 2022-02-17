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

## configuration
swagger options can be configured as you'd typically do like follows:
```csharp
builder.Services.AddSwaggerDoc(settings =>
{
    settings.Title = "My API";
    settings.Version = "v1";
});
```

## describe endpoints
by default, both `Accepts` and `Produces` are inferred from the request/response dto types of your endpoints and added to the swagger description. if the defaults are not satisfactory, you can clear the defaults completely and describe your endpoints with the `Describe()` method in configuration like so:
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
          .ProducesProblem(403));
    }
}
```
on the other-hand if the default `Accepts` & `Produces` are ok, and you want to just add something else like `ProducesProblem`, simply use the `Options()` method to specify the additional descriptions:
```csharp
public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Options(x => x.ProducesProblem(404));
}
```

### swagger documentation

the text descriptions for the endpoint and the different responses the endpoint returns can be specified with the `Summary()` method:
```csharp
public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Describe(b => b
      .Accepts<MyRequest>("application/json")
      .Produces<MyResponse>(200,"application/json")
      .ProducesProblem(403));
    Summary(s => {
        s.Summary = "short summary goes here";
        s.Description = "long description goes here";
        s[200] = "success response description goes here";
        s[403] = "forbidden response description goes here";
    });      
}
```

if you prefer to move the summary text out of the endpoint class, you can do so by subclassing the `EndpointSummary` type:
```csharp
class AdminLoginSummary : EndpointSummary
{
    public AdminLoginSummary()
    {
        Summary = "short summary goes here";
        Description = "long description goes here";
        this[200] = "success response description goes here";
        this[403] = "forbidden response description goes here";
    }
}

public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Describe(b => b
      .Accepts<MyRequest>("application/json")
      .Produces<MyResponse>(200,"application/json")
      .ProducesProblem(403));
    Summary(new AdminLoginSummary());        
}
```

### xml documentation
xml documentation is only supported for request/response dtos (swagger schemas) which can be enabled by adding the following to the `csproj` file:
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>CS1591</NoWarn>
</PropertyGroup>
```
> if you can figure out how to get nswag to read the xml summary/remarks tags from the endpoint classes, please submit a PR on github.

## disable jwt auth scheme
support for jwt bearer auth is automatically added. if you need to disable it, simply pass a `false` value to the following parameter:
```csharp
builder.Services.AddSwaggerDoc(addJWTBearerAuth: false);
```

## multiple authentication schemes
multiple global auth scheme support can be enabled by using the `AddAuth()` method like below.
```csharp
builder.Services.AddSwaggerDoc(s =>
{
    s.DocumentName = "Release 1.0";
    s.Title = "Web API";
    s.Version = "v1.0";
    s.AddAuth("ApiKey", new()
    {
        Name = "api_key",
        In = OpenApiSecurityApiKeyLocation.Header,
        Type = OpenApiSecuritySchemeType.ApiKey,
    });
    s.AddAuth("Bearer", new()
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
    });
});
```
doing the above will associate each of the auth schemes with all endpoints/ swagger operations. if some of your endpoints are only using a a few, they can be specified per endpoint with the `AuthSchemes()` [endpoint method](Security.md#multiple-authentication-schemes), in which case only the relevant auth schemes will be associated with each swagger operation. for example, if you have both `ApiKey` and `Bearer` schemes enabled in swagger and an endpoint only uses `ApiKey` scheme, when you hit the `Try It Out` button in swagger ui, only api key auth prompt will be shown.

## group endpoints by path segment
if you'd like to group your endpoints by a segment of the route url, simply specify an integer indicating which segment to use for tagging/grouping.
```csharp
builder.Services.AddSwaggerDoc(tagIndex: 2)
```

## customize swagger schema names
by default, schema names are generated using the full name of dto classes. you can make the schema names be just the class name.
```cshar
builder.Services.AddSwaggerDoc(shortSchemaNames: true);
```

## swagger serializer options
nswag uses a separate serializer (newtonsoft) and has it's own set of serializer options you can configure like so:
```csharp
builder.Services
    .AddSwaggerDoc(serializerSettings: x =>
    {
        x.ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy() //set to null for pascal case
        };
    })
```

## customize endpoint name/ swagger operation id
the full name (including namespace) of the endpoint classes are used to generate the operation ids. you can change it to use just the class name by doing the following at startup:
```csharp
app.UseFastEndpoints(c =>
{
    c.ShortEndpointNames = true;
});
```

if the auto-generated operation ids are not to your liking, you can specify a name for an endpoint using the `WithName()` method.
```csharp
public override void Configure()
{
    Get("/sales/invoice/{InvoiceID}");
    Options(x => x.WithName("GetInvoice"));
}
```
**note:** when you manually specify a name for an endpoint like above and you want to point to that endpoint when using [SendCreatedAtAsync()](Misc-Conveniences.md#sendcreatedatasync) method, you must use the overload that takes a string argument with which you can specify the name of the target endpoint. i.e. you lose the convenience/type-safety of being able to simply point to another endpoint using the class type like so:
```csharp
await SendCreatedAtAsync<GetInvoiceEndpoint>(...);
```
instead you must do this:
```csharp
await SendCreatedAtAsync("GetInvoice", ...);
```