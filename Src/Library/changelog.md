### CHANGES
- signature of global error response builder func has changed to include the `HttpContext` [#docs](https://fast-endpoints.com/docs/configuration-settings#customizing-error-responses)
- security related methods such as `ep.Roles(...)` in global config will now compound what's being done in the endpoint config [#docs](https://fast-endpoints.com/docs/configuration-settings#global-endpoint-options)

### NEW
- support .net 7.0 via multi targeting
- endpoint configuration with groups and sub/nested groups [#docs](https://fast-endpoints.com/docs/configuration-settings#endpoint-configuration-groups)
- complex object binding from json object strings for query/forms/claims/headers [#docs](https://fast-endpoints.com/docs/model-binding#json-objects)
- complex object binding from query parameters #238 #245 #254 #266
- `[Throttle(...)]` attribute for configuring endpoints #227
- min endpoint version support for `AddSwaggerDoc()` #244
- ability to filter out non-fastendpoints from swagger docs #246
- non-conforming DI container support #243
- endpoint unit testing support for attribute based config [#info](https://discord.com/channels/933662816458645504/1021479855130427442)
- asymmetric jwt signing support in `FastEndpoints.Security` pkg #249
- add `EndpointVersion()` method to `EndpointDefinition` for use with global config #209
- filtering (endpoint inclusion) for swagger documents #252
- specify response examples with `EndpointSummary` #205
- specify dto property level examples with xml comments #276
- specify endpoint summary and description with xml comments [#info](https://fast-endpoints.com/docs/swagger-support#enabling-xml-documentation)
- `TokenValidationParameters` config action argument for `AddAuthenticationJWTBearer()` method #268
- `HttpContext.MarkResponseStart()` and `HttpContext.ResponseStarted()` extension methods #230

### FIXES
- pre/post processor collection modification bug #224
- response dto initialization not working with array types #225
- unable to instantiate validators for unit tests [#info](https://discord.com/channels/933662816458645504/1017889876521267263)
- nested schema resolving in nswag operation processor [#info](https://discord.com/channels/933662816458645504/1018565805555863572)
- concurrent test execution bug #224
- workaround for grpc wildcard route match conflict [#info](https://discord.com/channels/933662816458645504/1020806973689696388)
- plain text request fails if request contains json content type [#info](https://discord.com/channels/933662816458645504/1021819753016328253)
- nre when publishing an event and no handlers are registered #259

### IMPROVEMENTS
- remove `notnull` constraint from `TResponse` generic argument of endpoint class
- `Logger` endpoint property now uses `ILoggerFactory` to create loggers
- apply validation rules from included/foreach validators #270
- json object array string binding of requests from swagger ui
- optimize default request binder by reducing allocations
- better swagger schema resolving