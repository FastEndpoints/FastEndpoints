### NEW
- command bus pattern request/response messaging (independant of endpoints) #294
- dependency resolving support for endpoint `Configure()` method
- constructor injection support for event handlers
- type safety for the shortcut http verb methods such as `Get()`, `Post()`, etc. [#info](https://fast-endpoints.com/docs/misc-conveniences#shorthand-route-configuration)
- custom value parser registration at startup for any given type #285
- specify whether to execute global pre/post processors before or after endpoint level processors #291
- `[DontInject]` attribute for preventing property injection of endpoint properties
- add `Verbs(...)` overload that can take any string #299

### IMPROVEMENTS
- remove read-only properties from swagger request body #283
- move attribute classes to a separate package `FastEndpoints.Attributes` [#info](https://discord.com/channels/933662816458645504/955771546654359553/1032020804671647854)
- non-conforming DI container support #289
- remove previously deprecated scoped validator support
- make `IEventHandler<TEvent>` public and remove requirement of `FastEventHandler<TEvent>`

### FIXES
- swagger response examples not honoring serializer settings #280
- swagger request property xml examples not picked up for route params #287 
- property injection not working on sub-classes #292