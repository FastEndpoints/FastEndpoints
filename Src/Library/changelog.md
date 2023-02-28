### NEW
- support for `record` types and `required` keyword by removing `new()` constraint on request DTOs [#info]()
- allow command handlers to manipulate endpoint validation state [#info](https://discord.com/channels/933662816458645504/1076434477246586941)
- ability to automatically add produces 400 metadata for endpoints with validators [#info](https://discord.com/channels/933662816458645504/1077784720051556473)
- ability to remove optional `[FromHeaders],[FromClaim],[HasPermission]` annotated properties from swagger request schema #387
- support for `IParsable<T>` interface from .net 7.0 #385
- support for ignoring `GET` request DTO properties annotated with `[JsonIgnore]` attribute for disabling binding [#info](https://discord.com/channels/933662816458645504/1078782887756824606/1079980820581859378)

### IMPROVEMENTS
- better swagger support for OAS2.0 #390
- testing extensions now return a `TestResult<TResponse>` record instead of a value tuple
- optimize the endpoint request handler internals

### FIXES
- handling of STJ exceptions when the error is not directly related to any field of the json payload #391