### BREAKING CHANGES
- integration testing extensions (GETAsync(), POSTAsync(), etc.) no longer throws any exceptions
> if your integration tests are doing try/catch blocks for doing assertions on the unhappy/error path, those tests would now fail. 
you'll have to get rid of the try/catch blocks and assert on the `HttpResponseMessage` properties instead. 
the `TResponse` dto would now be `null` when the request is unsuccessful. 
here's an [example](https://github.com/FastEndpoints/FastEndpoints/blob/4831acea19f8b574bf7e4ebfe390ec4138a2a7e1/Tests/IntegrationTests/FastEndpoints.IntegrationTests/WebTests/AdminTests.cs#L65-L94).

### NEW
- `ValidationContext<T>` class for manipulating the validation failures list of the current endpoint [#info](https://discord.com/channels/933662816458645504/1090551226598432828)
- `RFC8707` compatible problem detail (error response) builder [#info](https://discord.com/channels/933662816458645504/1093917953528971344)
- `JsonExceptionTransformer` func to enable customization of error messages when STJ throws due to invalid json input [#info](https://discord.com/channels/933662816458645504/1095670893113528370/1095923891605622884)
- `ClearDefaultProduces(200,401,401)` extension method to clear chosen produces metadata added by default #432
- `MarkNonNullablePropsAsRequired()` swagger doc extension for TS client generation with OA3 swagger definitions #388
- json array request body binding for inherited `IEnumerable<T>` request DTOs #436

### IMPROVEMENTS
- add overload to `AddError()`, `ThrowError()`, `ThrowIfAnyErrors()` methods to accept a `ValidationFailure` [#info](https://discord.com/channels/933662816458645504/1090551226598432828/1090934715952926740)
- populate inner exception of `InvalidOperationException` thrown by the testing extensions #422
- modify source generator for incremental generation #426 
- automatically add `400 - Bad Request`, `401 - Unauthorized` and `403 - Forbidden` produces metadata to endpoints by default #432 
- upgrade dependencies to latest

### FIXES
- default response serializer func overriding the `content-type` of 400 responses [#info](https://discord.com/channels/933662816458645504/1090697556549447821)
- swagger descriptions for properties decorated with `[FromHeader("...")]` not being picked up [#info](https://discord.com/channels/933662816458645504/1093846313201827940)
- swagger descriptions for inherited classes/schema not being picked up