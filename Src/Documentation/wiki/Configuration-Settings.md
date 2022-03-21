# customizing functionality
there are several areas you can customize/override the default functionality of the library. 
all configuration settings must be specified during app startup with the `UseFastEndpoints()` call.

## specify json serializer options
the settings for the default json serializer which is `System.Text.Json` can be set like so:
```csharp
app.UseFastEndpoints(c =>
{
    c.SerializerOptions = o =>
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // set null for pascal case
    };
});
```

## global route prefix
you can have a specified string automatically prepended to all route names in your app instead of repeating it in each and every route config method by specifying the prefix at app startup.
```csharp
app.UseFastEndpoints(c =>
{
    c.RoutingOptions = o => o.Prefix = "api";
});
```
for example, the following route config methods would result in the below endpoint routes:
```csharp
Get("client/update"); -> "/api/client/update"
Put("inventory/delete"); -> "/api/inventory/delete"
Post("sales/recent-list"); -> "/api/sales/recent-list"
```
if needed, you can override or disable the global prefix from within individual endpoints like so:
```csharp
public override void Configure()
{
    Post("user/create");
    RoutePrefixOverride("mobile");
}
```
in order to disable the global prefix, simply pass in a `string.Empty` to the `RoutePrefixOverride()` method.

## filtering endpoint auto registration
if you'd like to prevent some of the endpoints in your project to be not auto registered during startup, you have the option to supply a filtering function which will be run against each discovered endpoint. if your function returns `true`, that particular endpoint will be registered. if the function returns `false` that endpoint will be ignored and not registered.
```csharp
app.UseFastEndpoints(c =>
{
    c.EndpointRegistrationFilter = ep =>
    {
        if (ep.Verbs.Contains("GET") && ep.Routes.Contains("/api/mobile/test"))
            return false; // don't register this endpoint

        return true;
    };
});
```
it is also possible to set a `Tag` for an endpoint and use that tag to filter out endpoints according to tags during registration as shown below:
```csharp
public override void Configure()
{
    Get("client/update");
    Tags("Deprecated", "ToBeDeleted"); //has no relationship with swagger tags
}

app.UseFastEndpoints(c =>
{
    c.EndpointRegistrationFilter = ep =>
    {
        if (ep.Tags.Contains("Deprecated") is true)
            return false; // don't register this endpoint

        return true;
    };
});
```

## global endpoint options
you can have a set of common options applied to each endpoint by specifying an action for the `GlobalEndpointOptions` property of the configuration. 
the action you set here will be executed for each endpoint during startup. you can inspect the `EndpointDefinition` argument to check what the current endpoint is, if needed.
options to be applied to endpoints are performed on the `RouteHandlerBuilder` argument. the action you specify here is executed before `Options()` and `Description()` of each individual endpoint during registration. whatever you do here may get overridden or compounded by what you do in the `Configure()` method of each endpoint.
```csharp
app.UseFastEndpoints(c =>
{
    c.GlobalEndpointOptions = (endpoint, builder) =>
    {
        if (endpoint.Routes[0].StartsWith("/api/admin") is true)
        {
            builder
            .RequireHost("admin.domain.com")
            .Produces<ErrorResponse>(400, "application/problem+json");
        }
    };
});
```

## customizing error responses
if the default error response is not to your liking, you can specify a function to produce the exact error response you need. whatever object you return from that function will be serialized to json and sent to the client whenever there needs to be an error response sent downstream. the function will be supplied a collection of validation failures as well as a http status code you can use to construct your own error response object like so:
```csharp
app.UseFastEndpoints(c =>
{
    c.ErrorResponseBuilder = (failures, statusCode) =>
    {
        var list = new List<KeyValuePair<string, string>>();

        foreach (var err in failures)
            list.Add(new(err.PropertyName, err.ErrorMessage));

        return list;
    };
});
```

## customizing de-serialization of json
if you'd like to take control of how request bodies are deserialized, simply provide a function like the following. the function is supplied with the incoming http request object, the type of the dto to be created, json serializer context, and a cancellation token. deserialize the object how ever you want and return it from the function. do note that this function will be used to deserialize all incoming requests with a json body. it is currently not possible to specify a deserialization function per endpoint.

input parameters:
```yaml
HttpRequest: the http request object
Type: the type of the request dto
JsonSerializerContext?: nullable json serializer context
CancellationToken: a cancellation token
```

```csharp
config.RequestDeserializer = async (req, tDto, jCtx, ct) =>
{
    using var reader = new StreamReader(req.Body);
    return Newtonsoft.Json.JsonConvert.DeserializeObject(await reader.ReadToEndAsync(), tDto);
};
```

## customizing serialization of response dtos
the response serialization process can be overridden by specifying a function that returns a `Task` object. you should set the content-type on the http response object and write directly to the response body stream. do note that this function will be used to serialize all outgoing responses where a json body is required. it is currently not possible to specify a serialization function per endpoint.

the parameters supplied to the function are as follows:

```yaml
HttpResponse: the http response object
object: the response dto to be serialized
string: the response content-type
JsonserializerContext?: nullable json serializer context
CancellationToken: a cancellation token
```

```csharp
config.ResponseSerializer = (rsp, dto, cType, jCtx, ct) =>
{
    rsp.ContentType = cType;
    return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
};
```