### NEW
- command bus pattern messaging [#info](https://fast-endpoints.com/docs/command-bus)
- ability to publish events from anywhere [#info](https://fast-endpoints.com/docs/event-bus#publish-from-anywhere)
- constructor injection support for event handlers [#info](https://fast-endpoints.com/docs/dependency-injection#event-handler-dependencies)
- type safety for the shortcut http verb methods such as `Get()`, `Post()`, etc. [#info](https://fast-endpoints.com/docs/misc-conveniences#strongly-typed-route-parameters)
- dependency resolving support for endpoint `Configure()` method
- custom value parser registration at startup for any given type [#info](https://fast-endpoints.com/docs/model-binding#custom-value-parsers)
- specify whether to execute global pre/post processors before or after endpoint level processors [#info](https://fast-endpoints.com/docs/pre-post-processors#global-processors)
- `[DontInject]` attribute for preventing property injection of endpoint properties
- add `Verbs(...)` overload that can take any string #299

### IMPROVEMENTS
- make `IEventHandler<TEvent>` public and remove requirement of `FastEventHandler<TEvent>`[#info](https://fast-endpoints.com/docs/event-bus#_2-define-an-event-handler)
- move attribute classes to a separate package `FastEndpoints.Attributes` [#info](https://discord.com/channels/933662816458645504/955771546654359553/1032020804671647854)
- remove read-only properties from swagger request body #283
- non-conforming DI container support #289
- remove previously deprecated scoped validator support

### FIXES
- swagger response examples not honoring serializer settings #280
- swagger request property xml examples not picked up for route params #287 
- property injection not working on sub-classes #292