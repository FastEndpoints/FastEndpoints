### CHANGE
- the signature of the global error response builder has changed to include the `HttpContext` #220 #230

### NEW
- `[Throttle(...)]` attribute for configuring endpoint #227
- `HttpContext.MarkResponseStart()` and `HttpContext.ResponseStarted()` extension methods #230
- endpoint configuration with groups and sub/nested groups #214
- complex model binding from json object strings for route/query/forms/headers #238

### FIX
- pre/post processor collection modification bug #224
- response dto initialization not working with array types #225
- unable to instantiate validators for unit tests [#info](https://discord.com/channels/933662816458645504/1017889876521267263)
- nested schema resolving in nswag operation processor [#info](https://discord.com/channels/933662816458645504/1018565805555863572)
