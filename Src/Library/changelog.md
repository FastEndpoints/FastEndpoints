### NEW
- global model binder support [#info](https://fast-endpoints.com/docs/model-binding#global-request-binder)
- global request binding modifier function [#info](https://fast-endpoints.com/docs/model-binding#binding-modifier-function)
- constructor injection support for mappers [#info](https://fast-endpoints.com/docs/dependency-injection#entity-mapper-dependencies)
- unit testing support for event handlers (successful execution & exceptions only) [#info](https://github.com/FastEndpoints/Library/blob/main/Tests/UnitTests/FastEndpoints.UnitTests/EventBusTests.cs)
- startup type discovery filter #203
- `DELETEAsync()` extension method for testing #216
- `AddSwaggerDoc(removeEmptySchema:true)` parameter for removing empty schemas from swagger document [#info](https://fast-endpoints.com/docs/swagger-support#removing-empty-schema)

### FIX
- global endpoint configurator ineffective for route prefix override and security related calls #207 [#info](https://discord.com/channels/933662816458645504/1012563507339857930)
- global configurator overriding endpoint level summary object #210 #212
- empty schema in swagger doc if under namespace [#info](https://discord.com/channels/933662816458645504/1014025472792870992)
- incorrect client generation with nswag [#info](https://discord.com/channels/933662816458645504/1014300348275499058)
- incorrect swagger doc/client generation when `[FromBody]` attribute was used [#info](https://discord.com/channels/933662816458645504/1017005911220428871)
- post processors were not executed if validation error was thrown by user code

### IMPROVEMENTS
- reworked event notification system
- improve service provider scoping
- optimize default request binder

### CHANGES
- deprecate ability to register validators as scoped in favor of `CreateScope()` method