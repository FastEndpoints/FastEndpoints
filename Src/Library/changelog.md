### NEW
- custom request model binder support via `IRequetBinder<TRequest>` interface
- `[FromBody]` attribute for binding requet json body to the dto sub property
- swagger integration for `[FromBody]` attribute
- support for binding duplicate query param values to `IEnumerable` properties #165
- `UpdateEntity()` method for mapper to udate an entity from the request dto #167

### FIXES
- swagger issue when `ApiController` exists in same project #163