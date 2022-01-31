# api versioning

the versioning strategy in FastEndpoints is simplified to require way less effort by the developer. basically, you evolve/version each endpoint in your project independently and group them into a `release number/name` using swagger. 

when it's time for an endpoint contract to change, simply leave the existing endpoint alone and create (either by inheriting the old one) or creating a brand new endpoint class and call the `Version(x)` method in the configuration to indicate that this is the latest incarnation of the endpoint.

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
a release group contains only the latest iteration of each endpoint in your project. all older/previous iterations will not show up. how to define release groups is described below.

# enable versioning
simply specify one of the `VersioningOptions` settings in startup config in order to activate versioning.
```csharp
app.UseFastEndpoints(c =>
{
    c.VersioningOptions = o => o.Prefix = "v";
});
```

# define swagger docs (release groups)
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
the thing to note here is the `maxEndpointVersion` parameter. this is where you specify the **max version** of an endpoint which a release group should include. any endpoint versions that are greater than this number will not be included in that release group/swagger doc. if you don't specify this, only the initial version of each endpoint will be listed in the group.

# mark endpoint with a version
```csharp
public class AdminLoginEndpoint_V2 : Endpoint<Request>
{
    public override void Configure()
    {
        Get("admin/login");
        Version(2);
    }
}
```

# deprecate an endpoint
you can specify that an endpoint should not be visible after (and including) a given version group like so:
```csharp
Version(1, deprecateAt: 4)
```
an endpoint marked as above will be visible in all swagger docs up until `maxEndpointVersion : 4`. it will be excluded from docs starting from `4` and above.
as an example, take to following two endpoints.

**initial release**
```shell
/user/delete
/user/profile
```

**release group v1.0**
```shell
/user/delete/v1
/user/profile/v1
```

**release group v2.0**
```shell
/user/delete/v1
/user/profile/v2
```
if you mark the `/user/delete/v1` endpoint with `Version(1, deprecateAt: 2)` then release groups `v2.0` and newer will not have any `/user/delete` endpoints listed. and the release will look like this:

**release group v2.0**
```shell
/user/profile/v2
```

it is only necessary to mark the last endpoint version as deprecated. you can leave all previous iterations alone, if there's any.

# versioning options
at least one of the following settings should be set in order to enable versioning support.

- **Prefix :** a string to be used in front of the version (for example 'v' produces 'v1')

- **DefaultVersion :** this value will be used for endpoints that do not specify a version in it's configuration. the default value is `0`. when the version of an endpoint is `0` it does not get added to the route making that version the initial version of that endpoint.

- **SuffixedVersion :** by default the version string is <b>*appended*</b> to the endpoint route. by setting this to `false`, you can have it <b>*prepended*</b> to the route.