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

## customizing error response

## customizing de-serialization of request dtos

## customizing serialization of response dtos
