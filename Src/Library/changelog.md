### NEW
- support for `IParsable<T>` interface from .net 7.0 #385
- ability to automatically add produces 400 metadata for endpoints with validators [#info](https://discord.com/channels/933662816458645504/1077784720051556473)
- ability to remove optional `[FromHeaders],[FromClaim],[HasPermission]` properties from swagger request schema #387

### IMPROVEMENTS
- testing extensions now return a `TestResult<TResponse>` record instead of a value tuple