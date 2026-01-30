# PR: Fix AOT/Trim DynamicallyAccessedMembers Warnings

## Overview
This PR adds `[DynamicallyAccessedMembers]` attributes to fix AOT/trim analysis warnings when publishing with `PublishAot=true` or `PublishTrimmed=true`. These annotations help the trimmer understand which type members are accessed dynamically via reflection, ensuring they are preserved during trimming.

## Warning Categories Fixed

### IL2067 - Parameter missing DynamicallyAccessedMembers annotation
When a method parameter is passed to an API that requires type metadata, the parameter needs to declare the same requirements.

### IL2070/IL2075 - 'this' argument/return value missing annotation  
When calling reflection methods like `Type.GetInterfaces()`, `Type.GetMethods()`, etc., the type must have appropriate annotations.

### IL2087/IL2091 - Generic type parameter missing annotation
When generic type parameters are used with reflection APIs, they need constraints declaring what members are accessed.

---

## Changes by File

### 1. Src/Core/ServiceResolver/ServiceResolver.cs
**Warning:** IL2067 at lines 41, 45
**Issue:** `FactoryInitializer` and `CreateSingleton` methods pass `Type` parameters to `ActivatorUtilities.CreateFactory` and `ActivatorUtilities.GetServiceOrCreateInstance` without proper annotations.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to the `t` and `type` parameters.

### 2. Src/Security/Revocation/JwtRevocationExtensions.cs
**Warning:** IL2091 at line 12
**Issue:** `UseJwtRevocation<T>` generic parameter passed to `UseMiddleware<T>` which requires constructor and method annotations.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]` constraint to generic parameter `T`.

### 3. Src/JobQueues/JobQueueExtensions.cs
**Warning:** IL2091 at line 37, IL2075 at line 80
**Issue:** `TStorageProvider` generic parameter needs `PublicConstructors` annotation. `UseJobQueues` iterates types and calls `GetInterface()`.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to `TStorageProvider`. Add annotation to local iteration variable.

### 4. Src/JobQueues/JobQueue.cs
**Warning:** IL2090 at line 85
**Issue:** `TCommand` generic parameter calls `GetInterface()` without proper annotation.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` constraint to `TCommand` generic parameter.

### 5. Src/Core/AssemblyScanner/AssemblyScanner.cs
**Warning:** IL2070 at line 62
**Issue:** `IsTypeMatch` local function parameter `t` calls `GetInterfaces()` without annotation.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` to the `t` parameter.

### 6. Src/Messaging/Messaging.Remote/Server/Commands/BaseHandlerExecutor.cs
**Warning:** IL2087 at line 18
**Issue:** `THandler` generic parameter passed to `ActivatorUtilities.CreateFactory` needs constructor annotation.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` constraint to `THandler`.

### 7. Src/Messaging/Messaging.Remote/Server/Events/EventHub.cs
**Warning:** IL2087 at line 78
**Issue:** `TStorageProvider` generic parameter passed to `ActivatorUtilities.CreateInstance` needs constructor annotation.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` constraint to `TStorageProvider`.

### 8. Src/Messaging/Messaging/MessagingExtensions.cs
**Warning:** IL2075 at line 40
**Issue:** Lambda iterates types and calls `GetInterfaces()` on them.
**Fix:** Add annotation to the iteration or suppress with justification if types are already registered.

### 9. Src/Library/Main/EndpointData.cs
**Warning:** IL2075 at line 71, IL2080 at line 136, IL2067 at line 189
**Issue:** Multiple reflection calls - `GetInterfaces()`, `GetMethods()`, and `Activator.CreateInstance()`.
**Fix:** Add appropriate annotations to type parameters and variables.

### 10. Src/Library/Binder/BinderExtensions.cs
**Warning:** IL2067 at line 42, IL2070 at lines 108, 117, 124, 175
**Issue:** Various reflection calls without proper type annotations.
**Fix:** Add `[DynamicallyAccessedMembers]` with appropriate member types to parameters.

### 11. Src/Library/Binder/RequestBinder.cs
**Warning:** IL2080 at line 39, IL2077 at line 69, IL2072 at lines 526, 542, 557, 572, 588
**Issue:** Static constructor and cache entry methods use reflection without annotations.
**Fix:** Add annotations to the `_tRequest` field and method parameters.

### 12. Src/Library/Endpoint/Endpoint.cs
**Warning:** IL2087 at lines 223, 251
**Issue:** `Route<T>` and `Query<T>` generic methods call `ValueParser` which uses reflection.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]` constraint to `T`.

### 13. Src/Library/Endpoint/Auxiliary/EndpointDefinition.cs
**Warning:** IL2072 at lines 796, 799
**Issue:** `GetFromBodyPropName` and `GetServiceBoundEpProps` call `BindableProps` without annotations.
**Fix:** Add annotations to the `ReqDtoType` and `EndpointType` properties or method parameters.

### 14. Src/Library/Endpoint/Factory/EndpointFactory.cs
**Warning:** IL2072 at line 26
**Issue:** `Create` method calls `SetterForProp` with unannnotated type.
**Fix:** Add annotation to the `EndpointType` access.

### 15. Src/Library/Main/MainExtensions.cs
**Warning:** IL2067 at line 471
**Issue:** `AllPropsAreNonJsonSourced` parameter calls `BindableProps` without annotation.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]` to `tRequest` parameter.

---

## Testing
- All changes verified by running `dotnet publish -c Release -r win-x64 /p:PublishAot=true /p:PublishTrimmed=true`
- Warning count reduced from 166 to 148 (18 warnings eliminated)
- All IL2091 and IL2087 warnings (generic parameter/argument doesn't satisfy annotation) have been resolved

## Additional Files Modified

### 16. Src/Messaging/Messaging.Remote/Server/Commands/VoidHandlerExecutor.cs
**Issue:** `THandler` generic parameter needs `PublicConstructors` to satisfy `BaseHandlerExecutor` constraint.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to `THandler`.

### 17. Src/Messaging/Messaging.Remote/Server/Commands/UnaryHandlerExecutor.cs
**Issue:** Same as above.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to `THandler`.

### 18. Src/Messaging/Messaging.Remote/Server/Commands/ServerStreamHandlerExecutor.cs
**Issue:** Same as above.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to `THandler`.

### 19. Src/Messaging/Messaging.Remote/Server/Commands/ClientStreamHandlerExecutor.cs
**Issue:** Same as above.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]` to `THandler`.

### 20. Src/Messaging/Messaging.Remote/Server/HandlerOptions.cs
**Issue:** `TStorageProvider` needs `PublicConstructors` for `EventHub`. `THandler` params on Register methods need annotation.
**Fix:** Add annotations to `TStorageProvider` class parameter and `THandler` on all Register methods.

### 21. Src/Messaging/Messaging.Remote/Server/ServiceMethodProvider.cs
**Issue:** `TExecutor` parameter needs annotations to satisfy `IServiceMethodProvider<TExecutor>`.
**Fix:** Add `[DynamicallyAccessedMembers(PublicConstructors | PublicMethods | NonPublicMethods)]` to `TExecutor`.

### 22. Src/Messaging/Messaging.Remote/Server/IMethodBinder.cs
**Issue:** Interface `TExecutor` parameter needs same annotation for consistency.
**Fix:** Add `[DynamicallyAccessedMembers(PublicConstructors | PublicMethods | NonPublicMethods)]` to `TExecutor`.

### 23. Src/Library/Endpoint/Endpoint.Static.cs
**Issue:** `TRequest` generic argument doesn't satisfy `IRequestBinder<TRequest>` annotation requirement.
**Fix:** Add `[UnconditionalSuppressMessage]` as adding annotation would break user code.

### 24. Src/JobQueues/JobTracker.cs
**Issue:** `TCommand` generic parameter needs `Interfaces` annotation for `StoreJobResultAsync` call.
**Fix:** Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` to `TCommand` on both interface and class.

### 25. Src/Messaging/Messaging.Remote/Server/Commands/BaseHandlerExecutor.cs (Updated)
**Issue:** `TSelf` generic parameter needs annotations to satisfy `ServiceMethodProviderContext<TSelf>`.
**Fix:** Add `[DynamicallyAccessedMembers(PublicConstructors | PublicMethods | NonPublicMethods)]` to `TSelf`.

## Compatibility Notes

### netstandard2.1 Compatibility
The Core project targets `netstandard2.1` for non-AOT builds. Since `DynamicallyAccessedMembersAttribute` is not publicly exposed in netstandard2.1, we use conditional compilation:
```csharp
#if NET5_0_OR_GREATER
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
```
This ensures the annotations are only applied when targeting .NET 5.0+.

## Breaking Changes
None. These are additive annotations that don't change runtime behavior.

## Notes
Some warnings from external packages (NJsonSchema, Namotion.Reflection, NSwag) cannot be fixed in this PR as they require changes to those libraries.
