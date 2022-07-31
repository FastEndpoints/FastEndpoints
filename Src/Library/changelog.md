### NEW
- custom request model binder support via `IRequetBinder<TRequest>` interface #172
- `[FromBody]` attribute for binding request json body to a dto sub property
- swagger integration for `[FromBody]` attribute
- support for binding duplicate query param values to `IEnumerable` properties #165
- support for binding duplicate headers to `IEnumerable` properties
- `UpdateEntity()` method for mapper to update an entity from the request dto #167

### FIXES
- swagger issue when `ApiController` exists in same project #163
- enum binding from route cause NRE when value is invalid or case not matched