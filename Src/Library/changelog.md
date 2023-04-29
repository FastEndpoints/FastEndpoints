### EXPERIMENTAL
- support for `Asp.Versioning.Http` package via `FastEndpoints.AspVersioning` #352

### IMPROVEMENTS
- trim down `AddSwaggerDoc()` calls with `Action<DocumentOptions>` argument. see deprecations below & [example here](https://github.com/FastEndpoints/FastEndpoints/blob/6563531e2b3ac02a159927ee4a61a310e6c6b5fb/Web/Program.cs#L20-L32).
- improve `IFormFile` support in OAS2 [#info](https://discord.com/channels/933662816458645504/1101429081830064162)
- nswag operation processor internal optimizations

### DEPRECATIONS
- `FastEndpoints.Swagger.Extensions.AddSwaggerDoc(...)` in favor of `AddSwaggerDoc(Action<DocumentOptions>)`
- `FastEndpoints.Swagger.Extensions.EnableFastEndpoints(...)` in favor of `EnableFastEndpoints(Action<DocumentOptions>)`