### CHANGES
- the signature of the global error response builder has changed to include the `HttpContext` #220 #230

### NEW
- endpoint configuration with groups and sub/nested groups #214
- `[Throttle(...)]` attribute for configuring endpoints #227
- `HttpContext.MarkResponseStart()` and `HttpContext.ResponseStarted()` extension methods #230
- complex object binding from json object strings for route/query/forms/headers #238
- simple object binding from query parameters #238
- nested object binding from query parameters #245
- min endpoint version support for `AddSwaggerDoc()` #244
- ability to filter out non-fastendpoints from swagger docs #246
- non-conforming DI container support #243
- endpoint unit testing support for attribute based config [#info](https://discord.com/channels/933662816458645504/1021479855130427442)

### FIXES
- pre/post processor collection modification bug #224
- response dto initialization not working with array types #225
- unable to instantiate validators for unit tests [#info](https://discord.com/channels/933662816458645504/1017889876521267263)
- nested schema resolving in nswag operation processor [#info](https://discord.com/channels/933662816458645504/1018565805555863572)
- concurrent test execution bug #224
- workaround for grpc wildcard route match conflict [#info](https://discord.com/channels/933662816458645504/1020806973689696388)

### IMPROVEMENTS
- optimize default request binder by reducing allocations.
- swagger schema resolving
- json object array string binding of requests from swagger ui