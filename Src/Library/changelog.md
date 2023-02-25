### NEW
- allow command handlers to manipulate endpoint validation state [#info](https://discord.com/channels/933662816458645504/1076434477246586941)
- ability to automatically add produces 400 metadata for endpoints with validators [#info](https://discord.com/channels/933662816458645504/1077784720051556473)
- ability to remove optional `[FromHeaders],[FromClaim],[HasPermission]` annotated properties from swagger request schema #387
- support for `IParsable<T>` interface from .net 7.0 #385

### IMPROVEMENTS
- testing extensions now return a `TestResult<TResponse>` record instead of a value tuple
- optimize the endpoint request handler internals