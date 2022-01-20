# customizing functionality
there are several areas you can customize and override the default functionality of the library as described below. 
all configuration settings must be specified during app started with the `UseFastEndpoints()` call.

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
you can have a specified string automatically prepended to all route names in your app instead of repeating it in each and every route config method by specifying the prefix at app startup like so:
```csharp
app.UseFastEndpoints(c =>
{
    c.RoutingOptions = o => o.Prefix = "api";
});
```
for example, the following route config methods would result in the below endpoint routes:
```csharp
Get("client/update"); // "/api/client/update"
Put("inventory/delete"); // "/api/inventory/delete"
Post("sales/recent-list"); // "/api/sales/recent-list"
```

## filtering endpoint auto registration
if you'd like to prevent some of the endpoints in your project to be not auto registered during startup, you have the option to supply a filtering function which will be run against each discovered endpoint. if your function returns `true`, that particular endpoint with be registered. if the function returns `false` that endpoint will be ignored and not be registered.
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
it is also possible to set a `Tag` for an endpoint and then use that tag to filter out those endpoints during registration as shown below:
```csharp
public override void Configure()
{
    Get("client/update");
    Tags("Deprecated", "ToBeDeleted");
}

app.UseFastEndpoints(c =>
{
    c.EndpointRegistrationFilter = ep =>
    {
        if (ep.Tags?.Contains("Deprecated") is true)
            return false; // don't register this endpoint

        return true;
    };
});
```

## customizing error responses
if the default error response is not to your liking, you can specify a function to produce the exact error response you need. whatever object you return from that function will be serialized to json and sent to the client whenever there needs to be an error response sent downstream. the function will be supplied a collection of validation failures you can use to construct your own error response object like so:
```csharp
app.UseFastEndpoints(c =>
{
    c.ErrorResponseBuilder = failures =>
    {
        var list = new List<KeyValuePair<string, string>>();

        foreach (var err in failures)
            list.Add(new(err.PropertyName, err.ErrorMessage));

        return list;
    };
});
```

## customizing de-serialization of request dtos
if you'd like to take control of how request dtos are deserialized, simply provide a function like the following. the function is supplied with the incoming http request object, the type of the dto and a cancellation token. deserialize the object how ever you want and return it from the function. do note that this function will be used to deserialize all incoming requests. it is currently not possible to specify a deserialization function per endpoint.
```csharp
config.RequestDeserializer = async (req, tDto, ct) =>
{
    using var reader = new StreamReader(req.Body);
    return Newtonsoft.Json.JsonConvert.DeserializeObject(await reader.ReadToEndAsync(), tDto);
};
```

## customizing serialization of response dtos
the response serialization process can be overridden by specifying a function that returns a `Task` object. you should set the content-type on the http response object and write directly to the response body stream. do note that this function will be used to serialize all outgoing responses. it is currently not possible to specify a serialization function per endpoint.

the parameters supplied to the function are as follows:

```yaml
HttpResponse: the http response object
object: the response dto to be serialized
string: the response content-type
CancellationToken: a cancellation token
```

```csharp
config.ResponseSerializer = (rsp, dto, cType, ct) =>
{
    rsp.ContentType = cType;
    return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
};
```