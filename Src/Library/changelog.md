### CHANGES
- the signature of the global error response builder has changed to include the `HttpContext` #220 #230

### NEW
- endpoint configuration with groups and sub/nested groups #214
- `[Throttle(...)]` attribute for configuring endpoint #227
- `HttpContext.MarkResponseStart()` and `HttpContext.ResponseStarted()` extension methods #230
- complex object binding from json object strings for route/query/forms/headers #238
- simple object binding from query parameters #238

### FIXES
- pre/post processor collection modification bug #224
- response dto initialization not working with array types #225
- unable to instantiate validators for unit tests [#info](https://discord.com/channels/933662816458645504/1017889876521267263)
- nested schema resolving in nswag operation processor [#info](https://discord.com/channels/933662816458645504/1018565805555863572)
- concurrent test execution bug #224

### IMPROVEMENTS
- optimize default request binder by reducing allocations.
- swagger schema resolving
- json object array string binding of requests from swagger ui