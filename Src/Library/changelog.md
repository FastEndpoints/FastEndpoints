### NEW
- ability to call `PublishAsync()` on a `IEvent` type without a concrete event model type [#info](https://discord.com/channels/933662816458645504/1104729873743872170)
- `.UseSwaggerGen(uiConfig: c => c.DeActivateTryItOut())` extension method to de-activate the try it out button by default.

### IMPROVEMENTS
- prevent `UseFastEndpoint()` call overwriting the DI registered `JsonOptions` [#info](https://discord.com/channels/933662816458645504/1103132906681012295)

### FIXES
- swagger schema properties marked as required by a validator were not removed from the schema in some cases [#info](https://discord.com/channels/933662816458645504/1101429081830064162)