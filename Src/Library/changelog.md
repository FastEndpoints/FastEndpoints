---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## Breaking Changes ⚠️

## New 🎉

<details><summary>Bi-directional (duplex) streaming for remote messaging</summary>

You can now open a gRPC stream where the client keeps sending items while receiving results from the handler server at the same time. Mark the item type with `IDuplexStreamCommand<TResult>`, implement an `IDuplexStreamCommandHandler<TCommand, TResult>`, and register it on both sides:

```cs
// handler server
app.MapHandlers(h => h.RegisterDuplexStream<Ping, PingHandler, Pong>());

// client
app.MapRemote("http://localhost:6000", c => c.RegisterDuplexStream<Ping, Pong>());

await foreach (var pong in pings.RemoteExecuteDuplexAsync<Ping, Pong>(ct))
    Console.WriteLine(pong.Number);
```

See the [remote procedure calls documentation](https://fast-endpoints.com/docs/remote-procedure-calls#duplex-bi-directional-streaming) for details.

</details>

<details><summary>Publish once to matching base-class and interface event handlers</summary>

If your events share a base class or interface, you can now deliver one publish to every compatible subscription:

```cs
app.Services.UseMessaging(o => o.Events.EnableInheritance = true);
app.UseFastEndpoints();
```

This is opt-in and also works with standalone messaging.

Test event receivers follow the same rule. `GetTestEventReceiver<OrderCreated>()` sees an `OrderCreated` even if your code published it through a `DomainEvent` variable, and receivers for its base classes and interfaces see it as well.

</details>

## Improvements 🚀

<details><summary>Faster serializer context generation</summary>

With `GenerateSerializerContexts` enabled, common properties like `Guid`, `DateTime` or `IFormFile`, and endpoints using `EmptyRequest`/`EmptyResponse`, no longer make the generator scan your referenced projects and NuGet packages. If your DTOs live in the main project and only use such common types, the generator now skips that work on every regeneration, and the up-to-date check on later builds gets faster too. When DTOs from other projects or packages are actually needed, both lookups now run in parallel.

If a DTO from another project or package has the same name as one of these common types (for example your own `Results` or `Stream` class), refer to it with its namespace (`Contracts.Results`) or a `using` alias, so the generator doesn't take it for the built-in type.

</details>

<details><summary>Serializer context generator now matches the other generators' namespace</summary>

For projects with a dotted assembly name like `My.Cool.App`, the generated `AddSerializerContextsFrom*()` method used to end up in a different namespace (`My_Cool_App`) than `DiscoveredTypes` and `AddFrom*()`, so the AOT setup from the docs didn't compile. It now uses the assembly name for the namespace and strips non-alphanumeric characters from the method name, just like `AddFrom*()`:

```cs
c.Serializer.Options.AddSerializerContextsFromMyCoolApp();
c.Binding.ReflectionCache.AddFromMyCoolApp();
```

If your assembly name contains characters like `.`, `_` or `-`, or you set a `<RootNamespace>` or `<AssemblyName>` that differs from the project file name, update the method call after rebuilding.

</details>

<details><summary>Clearer startup error when an assembly can't be scanned</summary>

If one of your assemblies has a type whose dependency is missing at runtime (for example a design-time `IDesignTimeDbContextFactory` that references a private `Microsoft.EntityFrameworkCore.Design` package), type discovery used to fail with a bare `ReflectionTypeLoadException`. The error now names the assembly and the missing dependencies. If that assembly has no FastEndpoints types, you can exclude it from scanning with an `AssemblyFilter`:

```cs
bld.Services.AddFastEndpoints(o => o.AssemblyFilter = a => a != typeof(MyDbContext).Assembly);
```

See the [configuration settings documentation](https://fast-endpoints.com/docs/configuration-settings#excluding-assemblies-from-scanning) for details.

</details>

## Fixes 🪲

<details><summary>Enum schemas lost their XML summary when a nullable property referred to them first</summary>

If the first property referring to an enum was nullable (`MyEnum?`), the enum's schema in the OpenAPI document had no description, even when other properties used the plain enum. Enums now get their XML summary as the description however they're referenced.

</details>

<details><summary>Source generator edge cases that broke builds or skipped code</summary>

The source generators now handle a number of situations that used to produce code that didn't compile, crashed a generator, or silently left something out:

- Generated code no longer breaks when your project declares a type or namespace that shadows a name it references, such as a class named after your assembly, or a `MyApp.Domain`/`MyApp.System` namespace.
- `AddFrom*()` no longer throws a "duplicate keys" error at startup for request DTOs with overridden properties. DTOs with keyword property names (`@class`) or several constructors with the same number of parameters now compile.
- Properties with a conditional `[JsonIgnore(Condition = ...)]` are now bindable in Native AOT apps, just like they are without AOT. Likewise, `[JsonIgnore]`, `[DontInject]` and `[KeyedService]` on a base property now also apply to its overrides.
- Request DTOs with `required` fields, `[JsonIgnore]`/`[DontInject]` or non-public-getter `required` properties (including set-only overrides), or a `[SetsRequiredMembers]` constructor now compile cleanly with `AddFrom*()` without making ignored properties bindable or overwriting values set by the constructor.
- Endpoints inheriting from your own generic base class, as well as `EndpointWithoutRequest<TResponse>` endpoints, now get reflection data for the right DTO.
- `AccessControl()` calls inside `if` blocks or lambdas are no longer skipped. Permission descriptions containing `<`, `>` or `&` no longer produce invalid XML docs.
- `[RegisterService<T>(...)]` no longer crashes the generator when you write the lifetime as `Scoped` via `using static`. It now also works on record classes and with the fully qualified attribute name.
- `GenericProcessorTypes` now compiles in projects without implicit usings and in projects with generic endpoint classes. It also covers endpoints without a request DTO, and skips DTOs that don't satisfy an open generic processor's constraints (such as `where TRequest : IHasTenant`). Post-processors are only paired with request/response types your endpoints actually use, which keeps the generated file small in large projects.
- Handlers, validators and other discovered types declared as `record`s are now included in `DiscoveredTypes`.
- Assembly names with a segment that starts with a digit or is a C# keyword (like `2FA.Api`) now produce a valid namespace (`_2FA.Api`). This also applies to the serializer context generator.
- In the IDE, renaming DTO properties or changing an endpoint's request type no longer leaves stale generated code with bogus compile errors.
- With `GenerateSerializerContexts` enabled, the build now installs the generator CLI version matching your package once (downgrading a newer prerelease installed by older package versions), instead of running a tool install and update for the latest prerelease on every build.

</details>

<details><summary>Serializer context generator missed some DTO types</summary>

With `GenerateSerializerContexts` enabled, the generator now also registers:

- generic types and collection properties (`Result<T>`, `List<Order>`) of DTOs that come from NuGet packages. A package with both `Result` and `Result<T>` no longer causes a false "ambiguous external type reference" warning.
- DTOs from referenced projects whose name matches a built-in collection, such as your own `Queue` class.

Also, removing the last endpoint DTOs no longer leaves old generated files behind that break the build. The generated `AddSerializerContextsFrom*()` method then does nothing, so your startup code keeps compiling. Warnings found during the up-to-date check are now printed as well.

</details>

<details><summary>Default exception handler returned an empty body in Native AOT apps</summary>

In Native AOT apps, `UseDefaultExceptionHandler()` sent a 500 response with no body, because the error response type wasn't registered with the serializer. It now returns the usual JSON error response, the same as JIT apps.

</details>

<details><summary>Permission codes could depend on the machine's culture</summary>

The 3-character codes generated for permissions were upper-cased with the current culture. On a machine with a Turkish or Azerbaijani culture an `i` became `İ`, so a code could contain a non-ASCII letter, and the codes generated at build time by the source generator could differ from the ones computed at runtime if the two machines used different cultures. Both now use invariant casing, so the codes are always `A-Z0-9`.

If your app was built or run under a Turkish or Azerbaijani culture, any permission codes containing `İ` will now contain `I` instead. Re-issue any such codes you have stored (for example in a database or in issued tokens).

</details>

<details><summary>Test command receivers weren't safe under concurrent command execution</summary>

`GetTestCommandReceiver<T>().WaitForMatchAsync()` could throw a "Collection was modified" exception, or return results that kept changing afterwards, when commands were executed in parallel during a test (for example concurrent requests or gRPC calls). It now matches against a snapshot and returns a fixed result, the same as test event receivers.

</details>

<details><summary>Memory growth on handler servers serving client-stream commands</summary>

Each incoming client-stream RPC created a cancellation token source linked to the app's shutdown token and never disposed it. On long-running handler servers, every client-stream call left a small allocation behind until the app shut down. It's now disposed when the call finishes, like the server-stream and duplex paths already did.

</details>

<details><summary>Void command handlers couldn't see the 'ServerCallContext' during execution</summary>

If a remote "void" command handler (`ICommandHandler<TCommand>`) implemented `IHasServerCallContext`, its `ServerCallContext` property was only set after `ExecuteAsync` had already finished, so it was always `null` inside the handler. It's now set before execution, the same as for all the other command types.

</details>