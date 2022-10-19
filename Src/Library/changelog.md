### NEW
- type safety for the shortcut http verb methods such as `Get()`, `Post()`, etc. [#info](https://fast-endpoints.com/docs/misc-conveniences#shorthand-route-configuration)
- custom value parser registration at startup for any given type #285

### IMPROVEMENTS
- remove read-only properties from swagger request body #283
- move attribute classes to a separate package `FastEndpoints.Attributes` [#info](https://discord.com/channels/933662816458645504/955771546654359553/1032020804671647854)

### FIXES
- swagger response examples not honoring serializer settings #280