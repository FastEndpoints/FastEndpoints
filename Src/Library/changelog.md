### NEW
- ability to submit a request DTO as multipart form data when integration testing endpoints that accept form data. #409

### IMPROVEMENTS
- refactor & simplify httpclient extensions for testing 

### FIXES
- double response issue if pre-processor sent response and validator also has failures. [#info](https://discord.com/channels/933662816458645504/1080609437879914506)
- `EndpointWithMapping<TRequest, TResponse, TEntity>` map methods becoming protected in v5.8 [#info](https://discord.com/channels/933662816458645504/1082207914376319026)
- endpoint factory being inferred from request body issue [#info](https://discord.com/channels/933662816458645504/1084841217898061915)