### NEW
- shortcut `app.UseSwaggerGen()` as an alternative to `app.UseOpenApi() + app.UseSwagger()`
- choose swagger tag naming strategy with `AddSwaggerDoc()` method #330
- provide descriptions for response dto properties using the endpoint summary [#info](https://github.com/vpetrusevici/Library/blob/3467e90e17c8acfb80a3c94d78d28e4ba7177949/Web/%5BFeatures%5D/TestCases/QueryObjectBindingTest/Endpoint.cs#L17-L18)

### FIXES
- roles requirement issue with attribute based config [#info](https://discord.com/channels/933662816458645504/1038735848167968798)
- bug in command handlers and scoped service resolving #324

### IMPROVEMENTS
- reduce startup time
- validate swagger startup config order
- optimize value parser functionality
- service resolver refactors