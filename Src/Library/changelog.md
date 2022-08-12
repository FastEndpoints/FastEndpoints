### NEW
- add `ProducesProblemFE()` extension method for `RouteHandlerBuilder` [#info](https://discord.com/channels/933662816458645504/1004762111546769498)
- code snippets added to [visual studio extension](https://marketplace.visualstudio.com/items?itemName=dj-nitehawk.FastEndpoints)

### IMPROVEMENTS
- remove the `new()` contraint on response dtos so a parameterless ctor is not needed on response classes #184
- built-in unhandled exception handler now sends a response of type `InternalErrorResponse`
- increase logging in validation schema processor #117
- update dependencies to latest

### FIXES
- swagger schema becoming invalid overnight #173
- oversight in duplicate route detection code
