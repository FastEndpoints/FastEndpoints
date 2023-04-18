### NEW
- `ValidationContext<T>` class for manipulating the validation failures list of the current endpoint [#info](https://discord.com/channels/933662816458645504/1090551226598432828)
- `RFC8707` compatible problem detail (error response) builder [#info](https://discord.com/channels/933662816458645504/1093917953528971344)
- `JsonExceptionTransformer` func to enable customization of error messages when STJ throws due to invalid json input [#info](https://discord.com/channels/933662816458645504/1095670893113528370/1095923891605622884)

### IMPROVEMENTS
- add overload to `AddError()`, `ThrowError()`, `ThrowIfAnyErrors()` methods to accept a `ValidationFailure` [#info](https://discord.com/channels/933662816458645504/1090551226598432828/1090934715952926740)
- populate inner exception of `InvalidOperationException` thrown by the testing extensions #422
- modify source generator for incremental generation #426 
- upgrade dependencies to latest

### FIXES
- default response serializer func overriding the `content-type` of 400 responses [#info](https://discord.com/channels/933662816458645504/1090697556549447821)
- swagger descriptions for properties decorated with `[FromHeader("...")]` not being picked up [#info](https://discord.com/channels/933662816458645504/1093846313201827940)
- swagger descriptions for inherited classes/schema not being picked up