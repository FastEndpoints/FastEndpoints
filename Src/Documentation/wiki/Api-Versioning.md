# api versioning

the versioning strategy in FastEndpoints is simplified to require way less effort by the developer. basically, you evolve/version each endpoint in your project independently and group them into a `release number/name` using swagger. 

when it's time for an endpoint contract to change, you simply leave the existing endpoint alone and create (either by inheriting the old one) or creating a brand new endpoint class and call the `Version(x)` method in the configuration to indicate that this is the latest incarnation of the endpoint.

for example, let's assume the following:

### initial state
your app has the following endpoints
```shell
- /admin/login
- /inventory/order/{OrderID}
```

### after evolving an endpoint
```shell
- /admin/login
- /admin/login/v1
- /inventory/order/{OrderID}
```
at this point you can have 2 releases (swagger documents) that look like the following:
```shell
 - initial release
 |- /admin/login
 |- /inventory/order/{OrderID}
 
 - release 1.0
 |- /admin/login/v1
 |- /inventory/order/{OrderID}
```

### after another change
```shell
- /admin/login
- /admin/login/v1
- /admin/login/v2
- /inventory/order/{OrderID}
- /inventory/order/{OrderID}/v1
```
your releases can now look like this:
```shell
 - initial release
 |- /admin/login
 |- /inventory/order/{OrderID}
 
 - release 1.0
 |- /admin/login/v1
 |- /inventory/order/{OrderID}

 - release 2.0
 |- /admin/login/v2
 |- /inventory/order/{OrderID}/v1
```
a release group only displays the latest iteration of each endpoint in your project. this is done automatically by default. you can customize this behavior by writing your own `IDocumentProcessor` for nswag if the default strategy is not satisfactory.

# enable versioning
simply specify one of the `VersioningOptions` settings in startup config in order to activate versioning like so:
```csharp
app.UseFastEndpoints(c =>
{
    c.VersioningOptions = o => o.Prefix = "v";
});
```

# define swagger docs/ release groups
```csharp 
builder.Services
    .AddSwaggerDoc(s =>
    {
        s.DocumentName = "Initial Release";
        s.Title = "my api";
        s.Version = "v1.0";
    })
    .AddSwaggerDoc(maxEndpointVersion: 1, settings: s =>
    {
        s.DocumentName = "Release 1.0";
        s.Title = "my api";
        s.Version = "v1.0";
    })
    .AddSwaggerDoc(maxEndpointVersion: 2, settings: s =>
    {
        s.DocumentName = "Release 2.0";
        s.Title = "my api";
        s.Version = "v2.0";
    });
```
the thing to note here is the `maxEndpointVersion` parameter. this is where you specify the **max version** which a release group should include. if you don't specify this, all of the versions of all endpoints will be listed in that release group.