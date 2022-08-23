### BREAKING CHANGES
this major version jump introduces some minor breaking changes to the startup configuration.
#### **1. restructured configuration**
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
read the docs [here](https://fast-endpoints.com/docs/configuration-settings#customizing-functionality).

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
read the docs [here](https://fast-endpoints.com/docs/configuration-settings#global-endpoint-options)

### NEW
- code snippets added to visual studio extension #[info](https://fast-endpoints.com/docs/scaffolding#vs-code-snippets)
- `FastEndpoints.ClientGen` package for c# and typescript client generation with `NSwag` #[info](https://fast-endpoints.com/docs/swagger-support#api-client-generation)
- override default model binding logic by inheriting from `RequestBinder<TRequest>` class #[info](https://fast-endpoints.com/docs/model-binding#inherit-the-default-binder)
- global pre/post processor support [#info](https://fast-endpoints.com/docs/pre-post-processors#global-processors)
- `DontCatchExceptions()` method to enable custom exception handler middleware #186
- `IncludeAbstractValidators` startup flag for including validators inheriting `AbstractValidator<TRequest>` in auto registration #[info](https://fast-endpoints.com/docs/validation#abstract-validator-classes)
- `Validator<TValidafor>()` method for being explicit in the endpoint configuration #[info](https://fast-endpoints.com/docs/validation#abstract-validator-classes)
- `RequestMapper<TRequest,TEntity>` and `ResponseMapper<TResponse,TEntity>` classes #[info](https://fast-endpoints.com/docs/domain-entity-mapping#mapping-logic-in-a-separate-class)
- `EndpointWithMapper<TRequest, TMapper>` and `EndpointWithoutRequest<TResponse, TMapper>` classes #188
- `ProducesProblemFE()` extension method for `RouteHandlerBuilder` [#info](https://discord.com/channels/933662816458645504/1004762111546769498)
- ability to customize permissions claim type #187
- multiple route support for http attributes #129

### IMPROVEMENTS
- remove the `new()` contraint on response dtos so a parameterless ctor is not needed on response classes #184
- built-in unhandled exception handler now sends a response of type `InternalErrorResponse`
- `ValidationFailureException` class now has the failure details #186
- support for non-ascii chars in `Content-Disposition` header #[info](https://discord.com/channels/933662816458645504/1009356074983379004)
- warn user at start up if duplicate validators are found and user is not being explicit about which one to use #190
- increase logging in validation schema processor #117
- add jetbrains external annotations #191
- update dependencies to latest

### FIXES
- swagger schema becoming invalid overnight #173
- swagger example couldn't handle `IEnumerable` records #195
- oversight in duplicate route detection code
- minor issue in versioning system related to default version and swagger