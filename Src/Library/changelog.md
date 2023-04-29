### EXPERIMENTAL
- support for `Asp.Versioning.Http` package via `FastEndpoints.AspVersioning` #352

### IMPROVEMENTS
- trim down `AddSwaggerDoc()` calls with `Action<DocumentOptions>` argument. see deprecations below & [example here]().
- improve `IFormFile` support in OAS2 [#info](https://discord.com/channels/933662816458645504/1101429081830064162)
- nswag operation processor internal optimizations

### DEPRECATIONS
- `FastEndpoints.Swagger.Extensions.AddSwaggerDoc(...)` in favor of `EnableFastEndpoints(Action<DocumentOptions>)`
- `FastEndpoints.Swagger.Extensions.EnableFastEndpoints(...)` in favor of `EnableFastEndpoints(Action<DocumentOptions>)`