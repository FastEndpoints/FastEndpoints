### NEW
- code snippets added to [visual studio extension](https://marketplace.visualstudio.com/items?itemName=dj-nitehawk.FastEndpoints)
- override default model binding logic by inheriting from `RequestBinder<TRequest>` class #189
- `ProducesProblemFE()` extension method for `RouteHandlerBuilder` [#info](https://discord.com/channels/933662816458645504/1004762111546769498)
- `DontCatchExceptions()` method to enable custom exception handler middleware #186
- `RequestMapper<TRequest,TEntity>` and `ResponseMapper<TResponse,TEntity>` classes #188
- `EndpointWithMapper<TRequest, TMapper>` and `EndpointWithoutRequest<TResponse, TMapper>` classes #188
- ability to customize permissions claim type #187

### IMPROVEMENTS
- remove the `new()` contraint on response dtos so a parameterless ctor is not needed on response classes #184
- built-in unhandled exception handler now sends a response of type `InternalErrorResponse`
- `ValidationFailureException` class now has more details #186
- increase logging in validation schema processor #117
- add jetbrains external annotations #191
- update dependencies to latest

### FIXES
- swagger schema becoming invalid overnight #173
- oversight in duplicate route detection code
