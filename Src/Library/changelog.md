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

<details><summary>Source generators do less work on every edit</summary>

The source generators re-check the classes in your project each time you type in the IDE. They now rule out non-matching types before reading their attributes or building their full names, skip repeated work for types that show up in many DTO properties (such as `string` and collections), and only inspect your endpoints' DTOs for the generic processor list when your project has an open generic pre/post-processor. Large projects should see less background CPU use while editing. The generated code is unchanged.

</details>

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

<details><summary>Endpoints requiring x402 payment ran for free when exposed as MCP tools or A2A skills</summary>

Payment is checked by the `UseX402()` middleware, but MCP and A2A calls run the endpoint without going through the HTTP pipeline. So an endpoint with `RequirePayment(...)` that was also exposed with `McpTool(...)` or `A2ASkill(...)` handed out its content to agents without asking for payment. Endpoints that require payment now refuse to run unless `UseX402()` handled the request, so MCP and A2A calls to them fail with an error and the handler doesn't run. Paying for endpoints over MCP or A2A isn't supported yet.

The same check throws an exception if `UseX402()` is missing or doesn't run between routing and the endpoint, instead of silently serving paid endpoints for free. See the [x402 payments documentation](https://fast-endpoints.com/docs/x402-payments) for details.

</details>

<details><summary>JWT audience and issuer checks were turned off unless set with 'ValidAudience' / 'ValidIssuer'</summary>

`AddAuthenticationJwtBearer()` only turned on audience and issuer validation when you set the single `ValidAudience` or `ValidIssuer` value. If you configured them any other way, such as `o.Audience`, `ValidAudiences` or `ValidIssuers`, validation was quietly switched off, even if you also set `ValidateAudience = true` or `ValidateIssuer = true`. A token issued for another service or by another issuer was accepted as long as it was signed with the same key.

Audience validation is now on whenever `ValidAudience`, `ValidAudiences` or `Audience` is set, and issuer validation is on whenever `ValidIssuer` or `ValidIssuers` is set. If nothing is set, neither is validated, same as before.

If you set any of these values but your tokens carry a different or missing `aud`/`iss`, those requests will now get a 401 response. Make sure the tokens you issue match what you configured.

</details>

<details><summary>Server and client stream calls kept running on the server after the client stopped</summary>

If you stopped reading a server stream early (with `break` or an exception inside the `await foreach`), the call was never cancelled, so the handler on the server kept running for a client that had stopped listening until the host shut down. The same happened with client streams when your item stream threw partway through or an item failed to send. Each of these leaked a handler, its DI scope and an HTTP/2 stream.

Both now cancel the call on the server, the same way duplex streams already did, so a handler's cancellation token fires when the client stops reading or its item stream fails. The cancellation token passed to a client stream's `RemoteExecuteAsync()` is now also passed to the item stream, so cancelling it no longer waits for the item stream to produce its next item. A server stream call now also starts when you begin enumerating the results, rather than when `RemoteExecuteAsync()` is called, and each enumeration starts a new call.

</details>

<details><summary>Antiforgery validation was skipped for plain-text endpoints</summary>

If an endpoint with `EnableAntiforgery()` used a request DTO that implements `IPlainTextRequest` (such as `PlainTextRequest`), a request sent as `text/plain`, with any other non-form content type, or with none at all skipped token validation, but the body was still bound and the handler still ran. A cross-site page could exploit that with a plain HTML form using `enctype="text/plain"`. These endpoints are now always validated, whatever the content type.

A plain-text body can't carry the token as a form field, so clients of these endpoints must send it in the antiforgery header (`RequestVerificationToken` by default). Requests without a valid token now get a 400 response.

</details>

<details><summary>Antiforgery validation was skipped when the content type had parameters</summary>

On endpoints with `EnableAntiforgery()`, a request sent as `application/x-www-form-urlencoded; charset=utf-8` (or with any other parameters on that content type) skipped token validation, while the form was still bound and the handler still ran. That let cross-site requests through without a token. The same happened for types you listed in `UseAntiforgeryFE(additionalContentTypes: ...)`, so `application/json` didn't cover the common `application/json; charset=utf-8`. The middleware now compares the media type itself and ignores its parameters, both on the request and in your `additionalContentTypes` list, so listing either `application/json` or `application/json; charset=utf-8` covers both, plus any other parameters. Related types such as `application/problem+json` still need to be listed separately.

If you have clients that post such requests without an antiforgery token, they will now get a 400 response and need to send a valid token.

</details>

<details><summary>Permissions from 'this.AccessControl()' calls were missing from the generated Allow class</summary>

If you wrote `this.AccessControl(...)` or `base.AccessControl(...)` instead of a bare `AccessControl(...)` (for example because StyleCop or your `.editorconfig` requires `this.` qualification), the endpoint still required the permission at runtime, but the source generator left it out of the `Allow` class. Groups like `Allow.Admin` and `Allow.AllCodes()` silently lacked it, so users got 403s, and referencing the constant didn't compile. All three call styles are now picked up. Your own unrelated methods that happen to be named `AccessControl` are no longer mistaken for permissions.

</details>

<details><summary>Hand-written permissions missing from the generated Allow lookups</summary>

Permissions you add yourself to a partial `Allow` class were silently left out of `AllCodes()`, `AllNames()`, `CodesFor()`, `PermissionCodeFor()` and the other lookup methods when the class part wasn't marked `static`, the field was typed `String` instead of `string`, or one line declared several constants (`public const string A = "1", B = "2";`). They're all included again, as they were before v8.2. Partial permission properties typed `String` also get their generated implementation now instead of failing with CS9248. Names that need escaping no longer break the build either, like a partial property called `@class`, or a `static readonly` field called `permNames` or `permCodes`.

If two of your hand-written constants share the same code, the one that used to be skipped is now checked too, so the build may start reporting `FEAC001` for them.

Partial permission properties now get the same code as an `AccessControl()` call with the same name. This only changes the code of properties whose names contain consecutive underscores or non-ASCII letters (like `Foo__Bar` or `Café`). If you've stored codes for such properties in claims or a database, re-issue them, or keep the old code with a `const` field instead of the partial property.

</details>

<details><summary>Enum schemas lost their XML summary when a nullable property referred to them first</summary>

If the first property referring to an enum was nullable (`MyEnum?`), the enum's schema in the OpenAPI document had no description, even when other properties used the plain enum. Enums now get their XML summary as the description however they're referenced.

</details>

<details><summary>Source generator edge cases that broke builds or skipped code</summary>

The source generators now handle a number of situations that used to produce code that didn't compile, crashed a generator, or silently left something out:

- Generated code no longer breaks when your project declares a type or namespace that shadows a name it references, such as a class named after your assembly, or a `MyApp.Domain`/`MyApp.System` namespace.
- Endpoints, DTOs, validators, services and hand-written `Allow` fields marked `[Obsolete]` no longer cause obsolete warnings in the generated code, which used to break the build with `TreatWarningsAsErrors`. Before, only `[Obsolete("message")]` endpoints and DTOs were covered.
- `AddFrom*()` no longer throws a "duplicate keys" error at startup for request DTOs with overridden properties, or an "ambiguous match" error when a DTO (including nested ones) or an endpoint hides a base property with a `new` property of a different type. DTOs with keyword property names (`@class`) or several constructors with the same number of parameters now compile.
- Request DTOs with `dynamic` properties (including `dynamic[]` and `List<dynamic>`) no longer break the build with a CS1962 error in the generated reflection data.
- Properties with a conditional `[JsonIgnore(Condition = ...)]` are now bindable in Native AOT apps, just like they are without AOT. Likewise, `[JsonIgnore]`, `[DontInject]` and `[KeyedService]` on a base property now also apply to its overrides.
- Your own attributes that happen to be named `DontRegister` or `DontInject` no longer make the generators skip a type or property in Native AOT apps. Only the FastEndpoints attributes count, just like without AOT.
- Request DTOs with `required` fields, `[JsonIgnore]`/`[DontInject]` or non-public-getter `required` properties (including set-only overrides), or a `[SetsRequiredMembers]` constructor now compile cleanly with `AddFrom*()` without making ignored properties bindable or overwriting values set by the constructor.
- Endpoints inheriting from your own generic base class, as well as `EndpointWithoutRequest<TResponse>` endpoints, now get reflection data for the right DTO.
- `AccessControl()` calls inside `if` blocks or lambdas are no longer skipped. Permission descriptions containing `<`, `>` or `&` no longer produce invalid XML docs.
- A static class named `Allow` nested inside another class in your `{AssemblyName}.Auth` namespace is no longer mixed into the generated `Allow` class, where its constants showed up in the permission lookups and its other fields broke the build.
- `[RegisterService<T>(...)]` no longer crashes the generator when you write the lifetime as `Scoped` via `using static`. It now also works on record classes and with the fully qualified attribute name.
- If several classes use `[RegisterService<T>(...)]` for the same service with the same lifetime, `RegisterServicesFrom*()` now always adds them sorted by their full type name. Before, the order depended on the order of your source files, so the implementation you got when resolving a single instance could change when files were added or renamed. If you rely on a specific one winning, remove the attribute from the others.
- `GenericProcessorTypes` now compiles in projects without implicit usings and in projects with generic endpoint classes. It also covers endpoints without a request DTO, and skips DTOs that don't satisfy an open generic processor's constraints (such as `where TRequest : IHasTenant` or `where TResponse : IResult<TRequest>`). Post-processors are only paired with request/response types your endpoints actually use, which keeps the generated file small in large projects. `GenericProcessorTypes.All` is now also generated (as an empty list) when there are no open generic processors, so `AddFastEndpoints(DiscoveredTypes.All, GenericProcessorTypes.All)` keeps compiling when you remove your last one.
- Handlers, validators and other discovered types declared as `record`s are now included in `DiscoveredTypes`. Open generic pre/post-processors declared as `record`s are now included in `GenericProcessorTypes`, so they work in Native AOT apps too.
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