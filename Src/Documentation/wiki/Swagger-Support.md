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
swagger options can be configured as you'd typically do via the `AddSwaggerDoc()` method:
```csharp
builder.Services.AddSwaggerDoc(settings =>
{
    settings.Title = "My API";
    settings.Version = "v1";
});
```

## describe endpoints
by default, both `Accepts` and `Produces` metadata are inferred from the request/response dto types of your endpoints and added to the swagger document automatically. so you only need to specify the additional accepts/produces metadata using the `Description()` method like so:

```csharp
public class MyEndpoint : Endpoint<MyRequest, MyResponse>
{
    public override void Configure()
    {
        Post("/admin/login");
        AllowAnonymous();
        Description(b => b
          .Produces<ErrorResponse>(400,"application/json+problem")
          .ProducesProblem(403));
    }
}
```
if the default `Accepts` & `Produces` are not to your liking, you can clear the defaults and do it all yourself by setting the `clearDefaults` argument to `true`:
```csharp
public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Description(b => b
        .Accepts<MyRequest>("application/json+custom")
        .Produces<MyResponse>(200, "application/json+custom")
        .Produces<ErrorResponse>(400, "application/json+problem")
        .ProducesProblem(403),
    clearDefaults: true);
}
```

### swagger documentation

summary & description text, the different responses the endpoint returns, as well as an example request object can be specified with the `Summary()` method:
```csharp
public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Description(b => b
      .ProducesProblem(403));
    Summary(s => {
        s.Summary = "short summary goes here";
        s.Description = "long description goes here";
        s.ExampleRequest = new MyRequest { ... };
        s.Responses[200] = "success response description goes here";
        s.Responses[403] = "forbidden response description goes here";
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
        ExampleRequest = new MyRequest { ... };
        Responses[200] = "success response description goes here";
        Responses[403] = "forbidden response description goes here";
    }
}

public override void Configure()
{
    Post("/admin/login");
    AllowAnonymous();
    Description(b => b
      .ProducesProblem(403));
    Summary(new AdminLoginSummary());        
}
```

alternatively, if you'd like to get rid of all traces of `Summary()` from your endpoint classes and have the summary completely separated, you can implement the `Summary<TEndpoint>` abstract class like shown below:
```csharp
public class MySummary : Summary<MyEndpoint>
{
    public MySummary()
    {
        Summary = "short summary goes here";
        Description = "long description goes here";
        ExampleRequest = new MyRequest { ... };
        Response<MyResponse>(200, "ok response with body");
        Response<ErrorResponse>(400, "validation failure");
        Response(404, "account not found");
    }
}

public class MyEndpoint : Endpoint<MyRequest, MyResponse>
{
    public override void Configure()
    {
        Post("/api/my-endpoint");
        AllowAnonymous();
        //no need to specify summary here
    }
}
```
the `Response()` method does the same job as the `Produces()` method mentioned earlier. do note however, if you use the `Response()` method, the default `200` response is automatically removed, and you'd have to specify the `200` response yourself if it applies to your endpoint.

### describe request params
route parameters, query parameters and request dto property descriptions can be specified either with xml comments or with the `Summary()` method or `EndpointSummary` or `Summary<TEndpoint,TRequest>` subclassing. take the following for example:

**request dto:**
```csharp
/// <summary>
/// the admin login request summary
/// </summary>
public class Request
{
    /// <summary>
    /// username field description
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// password field description
    /// </summary>
    public string Password { get; set; }
}
```
**endpoint config:**
```csharp
public override void Configure()
{
    Post("admin/login/{ClientID?}");
    AllowAnonymous();
    Summary(s =>
    {
        s.Summary = "summary";
        s.Description = "description";
        s.Params["ClientID"] = "client id description");
        s.RequestParam(r => r.UserName, "overriden username description");
    });
}
```
use the `s.Params` dictionary to specify descriptions for params that don't exist on the request dto or when there is no request dto. 

use the `s.RequestParam()` method to specify descriptions for properties of the request dto in a strongly-typed manner. `RequestParam()` is also available when you use the `Summary<TEndpoint,TRequest>` generic overload.

whatever you specify within the `Summary()` method as above takes higher precedence over xml comments.

### enabling xml documentation
xml documentation is only supported for request/response dtos (swagger schemas) which can be enabled by adding the following to the `csproj` file:
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>CS1591</NoWarn>
</PropertyGroup>
```
> if you can figure out how to get nswag to read the xml summary/remarks tags from the endpoint classes, please submit a PR on github.

## adding query params to swagger document
in order to let swagger know that a particular request dto property is being bound from a query string parameter, you need to decorate that property with the `[QueryParam]` attribute like below. when you annotate a property with the `[QueryParam]` attribute, a [query parameter will be added](/images/swagger-queryparam.png) to the swagger document for that property.
```csharp
public class CreateEmployeeRequest
{
    [QueryParam]
    public string Name { get; set; } //bound from query string

    [QueryParam, BindFrom("id")]
    public string? ID { get; set; } //bound from query string

    public Address Address { get; set; } //bound from body
}
```
the `[QueryParam]` attribute does not affect the [model binding order](/wiki/Model-Binding.md) in any way. it is just a way to make swagger add a query param.

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

## group endpoints by path segment (auto tagging)
if you'd like to group your endpoints by a segment of the route url, simply specify an integer indicating which segment to use for tagging/grouping.
```csharp
builder.Services.AddSwaggerDoc(tagIndex: 2)
```

### overriding auto tagging
if you have auto tagging enabled but would like to prevent a particular endpoint from being auto tagged, you can call the `DontAutoTag()` method in endpoint configuration to prevent a tag based on a path segment from being added.

### manual tagging
if you'd like to take control of the tagging behavior, simply set `tagIndex: 0` to disable auto tagging of endpoints and specify a tag for each endpoint via `Description(x => x.WithTags("xyz"))` method.

## customize swagger schema names
by default, schema names are generated using the full name of dto classes. you can make the schema names be just the class name.
```csharp
builder.Services.AddSwaggerDoc(shortSchemaNames: true);
```

## swagger serializer options
even though nswag uses a separate serializer (newtonsoft) internally, we specify serialization settings for nswag using `System.Text.Json.JsonSerializerOptions` just so we don't have to deal with anything related to newtonsoft (until nswag fully switches over to System.Text.Json).
```csharp
builder.Services.AddSwaggerDoc(serializerSettings: x =>
{
    x.PropertyNamingPolicy = null;
    x.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    ...
});
```
with the above approach, `System.Text.Json` annotations such as `JsonIgnore` and `JsonPropertyName` on your dtos work out of the box.

## customize endpoint name/ swagger operation id
the full name (including namespace) of the endpoint classes are used to generate the operation ids. you can change it to use just the class name by doing the following at startup:
```csharp
app.UseFastEndpoints(c =>
{
    c.ShortEndpointNames = true;
});
```
### custom endpoint names
if the auto-generated operation ids are not to your liking, you can specify a name for an endpoint using the `WithName()` method.
```csharp
public override void Configure()
{
    Get("/sales/invoice/{InvoiceID}");
    Description(x => x.WithName("GetInvoice"));
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