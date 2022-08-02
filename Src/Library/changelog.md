### NEW
- custom request model binder support via `IRequetBinder<TRequest>` interface #172
- `[FromBody]` attribute for binding request json body to a dto sub property [#info](https://discord.com/channels/933662816458645504/998996342187753543/998996347925561505)
- swagger integration for `[FromBody]` attribute
- support for binding duplicate query param values to `IEnumerable` properties #165
- support for binding duplicate headers to `IEnumerable` properties
- `UpdateEntity()` method for mapper to update an entity from the request dto #167
- support for `[DefaultValue(...)]` attribute with swagger [#info](https://discord.com/channels/933662816458645504/1003942423174598656/1003942426584551514)

### FIXES
- swagger issue when `ApiController` exists in same project #163
- enum binding from route cause NRE when value is invalid or case not matched [#info](https://discord.com/channels/933662816458645504/1003272032789741621/1003272038200381560)