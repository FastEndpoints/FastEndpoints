# enable endpoint versioning
specify a `Prefix` and `DefaultVersion` for versioning like below. prefix is a string that will be prepended to the version number of an endpoint. default version is used automatically for endpoints that do not specify a version.
```csharp
app.UseFastEndpoints(c =>
{
    c.VersioningOptions = o =>
    {
        o.Prefix = "v";
        o.DefaultVersion = "1";
    };
});
```
once you enable versioning as above, all of your endpoints will be registered with the default version number automatically prepended to them like so:
```yaml
/v1/sales/order
/v1/sales/order/cancel
/v1/sales/invoice
```

you can specify the version of a particular endpoint with the `Version()` method:
```csharp
public override void Configure()
{
    Get("sales/order");
    Version("2");
}
```
now your endpoints will look like this:
```yaml
/v2/sales/order
/v1/sales/order/cancel
/v1/sales/invoice
```
### disable auto versioning
if you do not want endpoints that do not specify a version to be automatically versioned with the `DefaultVersion`, simply set the default version to `VersioningOptions.Common` like so:
```csharp
c.VersioningOptions = o =>
{
    o.Prefix = "v";
    o.DefaultVersion = VersioningOptions.Common;
};
```
doing so will enable endpoints to remain `un-versioned` or `common` to all api version groups which comes in handy with versioning in swagger as well as not break any existing clients if you've been developing your project without versioning and decide to enable it.

# versioning in swagger

# [NSwag](#tab/nswag)

you can create multiple swagger documents with separate version groups like below:
```csharp
builder.Services
    .AddNSwag(s =>
    {
        s.DocumentName = "v1";
        s.Title = "my api";
        s.Version = "v1.0";
        s.ApiGroupNames = new[] { "v1" };
    })
    .AddNSwag(settings: s =>
    {
        s.DocumentName = "v2";
        s.Title = "my api";
        s.Version = "v2.0";
        s.ApiGroupNames = new[] { "v2" };
    });
```
the above will create two swagger docs/definitions each containing only the endpoints that match the respective api version group. [see here](https://github.com/dj-nitehawk/FastEndpoints/blob/747b35325510bf6059a73c6825a4d5f9ef97540b/Web/Program.cs#L37-L49) for a working example. the important bit here is the `ApiGroupNames` property which takes care of filtering the right set of endpoints for that particular swagger definition. if that property is not set, all endpoints from all version groups will be shown.

if the goal is to get swagger to contain all endpoints of a particular version + all common/un-versioned endpoints, simply do the following:
```csharp
s.ApiGroupNames = new[] { "v2", VersioningOptions.Common };
```

# [Swashbuckle](#tab/swashbuckle)

you can create multiple swagger documents with separate version groups like below:
```csharp
builder.Services.AddSwashbuckle(o =>
{
   o.SwaggerDoc(
       documentName: "v1",
       info: new()
       {
           Title = "my api",
           Version = "1.0"
       },
       apiGroupNames: new[] { "v1" });
   o.SwaggerDoc(
       documentName: "v2",
       info: new()
       {
           Title = "my api",
           Version = "2.0"
       },
       apiGroupNames: new[] { "v2" });
});

app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
});
```
the above will create two swagger docs/definitions each containing only the endpoints that match the respective api version group. the important bit here is the `apiGroupNames` parameter which takes care of filtering the right set of endpoints for that particular swagger definition. if that argument is not correctly set, either all endpoints from all version groups will be shown or none at all.

if the goal is to get swagger to contain all endpoints of a particular version + all common/un-versioned endpoints, simply do the following:
```csharp
s.ApiGroupNames = new[] { "v2", VersioningOptions.Common };
```