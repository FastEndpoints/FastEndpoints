### NEW
- ability to submit a request DTO as multipart form data when integration testing endpoints that accept form data. #409

### IMPROVEMENTS
- ability to execute a non-concrete `ICommand` types #411
- refactor & simplify httpclient extensions for testing 

### FIXES
- `EndpointWithMapping<TRequest, TResponse, TEntity>` map methods becoming protected in v5.8 [#info](https://discord.com/channels/933662816458645504/1082207914376319026)
- double response issue if pre-processor sent response and validator also has failures. [#info](https://discord.com/channels/933662816458645504/1080609437879914506)
- endpoint factory being inferred from request body issue [#info](https://discord.com/channels/933662816458645504/1084841217898061915)
- incorrect swagger spec generation issue [#info](https://discord.com/channels/933662816458645504/1085966972560347237)
- minor oversight in accept metadata checking/caching per endpoint on first request [#info](https://discord.com/channels/933662816458645504/1085526696406548604/1087362733021872200)