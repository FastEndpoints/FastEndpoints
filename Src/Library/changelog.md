### NEW
- code snippets added to [visual studio extension](https://marketplace.visualstudio.com/items?itemName=dj-nitehawk.FastEndpoints)
- override default model binding logic by inheriting from `RequestBinder<TRequest>` class #189
- `ProducesProblemFE()` extension method for `RouteHandlerBuilder` [#info](https://discord.com/channels/933662816458645504/1004762111546769498)
- `DontCatchExceptions()` method to enable custom exception handler middleware #186
- `RequestMapper<TRequest,TEntity>` and `ResponseMapper<TResponse,TEntity>` classes #188
- `EndpointWithMapper<TRequest, TMapper>` and `EndpointWithoutRequest<TResponse, TMapper>` classes #188
- ability to customize permissions claim type #187

### IMPROVEMENTS
- remove the `new()` contraint on response dtos so a parameterless ctor is not needed on response classes #184
- built-in unhandled exception handler now sends a response of type `InternalErrorResponse`
- `ValidationFailureException` class now has more details #186
- increase logging in validation schema processor #117
- add jetbrains external annotations #191
- update dependencies to latest

### FIXES
- swagger schema becoming invalid overnight #173
- oversight in duplicate route detection code

### BREAKING CHANGES
this major version jump introduces some minor breaking changes to your app startup configuration.
#### **1. restructured configuration
the `Config` object structure has been re-organized and made more convenient to use.
```cs
app.UseFastEndpoints(c =>
{
    c.Serializer.Options.PropertyNamingPolicy = null;
    c.Endpoints.RoutePrefix = "api";
    c.Endpoints.ShortNames = false;
    c.Endpoints.Filter = ...
    c.Endpoints.Configurator = ...
    c.Versioning.Prefix = "V";
});
```

#### **2. reworked global endpoint configuration**
the `GlobalEndpointOptions` was removed in favor or `Endpoints.Configurator`. 
this will be the new place to configure globally applicable endpoint settings.
most of the same endpoint configuration methods are available for use on the `ep` (EndpointDefinition) argument as shown below:
```cs
app.UseFastEndpoints(c =>
{
    c.Endpoints.Configurator = ep =>
    {
        ep.AllowAnonymous();
        ep.Options(b => b.RequireHost("admin.domain.com"));
        ep.Description(b => b.Produces<ErrorResponse>(400));
    });
});
```