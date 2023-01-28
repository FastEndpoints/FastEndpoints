### NEW
- easy cookie auth support via `FastEndpoints.Security` package [#info](https://fast-endpoints.com/docs/security#cookie-based-authentication)
- add `SendInterceptedAsync()` method to endpoint class for compatibility with `FluentResults` package #365

### IMPROVEMENTS
- automatically send 415 response if endpoint specifies content-types but request doesn't have any content-type headers [#info](https://discord.com/channels/933662816458645504/1064900909181718590)
- better handling of inherited `ICommandHandler` [#info](https://discord.com/channels/933662816458645504/1067599463310446592)

### FIXES
- jwt refresh token renewal privileges were not correctly set if awaiting a task in the method #368