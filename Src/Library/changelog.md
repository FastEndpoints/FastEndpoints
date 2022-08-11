### NEW
- add `ProducesProblemFE()` extension method for `RouteHandlerBuilder` [#info](https://discord.com/channels/933662816458645504/1004762111546769498)
- built-in unhandled exception handler now sends a response of type `InternalErrorResponse`

### FIXES
- swagger schema becoming invalid overnight #173

### IMPROVEMENTS
- remove the `new()` contraint on response dtos so a parameterless ctor is not needed on response classes #184
- increase logging in validation schema processor #117
- update dependencies to latest