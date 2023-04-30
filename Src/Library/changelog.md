### EXPERIMENTAL
- support for `Asp.Versioning.Http` package via `FastEndpoints.AspVersioning` [#info](https://github.com/FastEndpoints/FastEndpoints/issues/352#issuecomment-1522188041)

### IMPROVEMENTS
- trim down `AddSwaggerDoc()` calls with `SwaggerDocument(Action<DocumentOptions>)`. see deprecations below & [example here](https://github.com/FastEndpoints/FastEndpoints/blob/925d96ccea6e01cdee408142be1855f3baf616be/Web/Program.cs#L20-L32).
- improve `IFormFile` support in OAS2 [#info](https://discord.com/channels/933662816458645504/1101429081830064162)
- nswag operation processor internal optimizations

### DEPRECATIONS
> the following methods have been deprecated but will still work until the next major version jump. you may however want to refactor them as shown in the [latest documentation](https://fast-endpoints.com/docs/swagger-support#enable-swagger).
- `FastEndpoints.Swagger.Extensions.AddSwaggerDoc()` in favor of `SwaggerDocument(Action<DocumentOptions>)`
- `FastEndpoints.Swagger.Extensions.EndpointFilter()` in favor of `DocumentOptions.EndpointFilter` property
- `FastEndpoints.Swagger.Extensions.TagDescriptions()` in favor of `DocumentOptions.TagDescriptions` property
- `FastEndpoints.Swagger.Extensions.EnableFastEndpoints()` in favor of `EnableFastEndpoints(Action<DocumentOptions>)`