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

<details><summary>Clearer startup error when an assembly can't be scanned</summary>

If one of your assemblies has a type whose dependency is missing at runtime (for example a design-time `IDesignTimeDbContextFactory` that references a private `Microsoft.EntityFrameworkCore.Design` package), type discovery used to fail with a bare `ReflectionTypeLoadException`. The error now names the assembly and the missing dependencies. If that assembly has no FastEndpoints types, you can exclude it from scanning with an `AssemblyFilter`:

```cs
bld.Services.AddFastEndpoints(o => o.AssemblyFilter = a => a != typeof(MyDbContext).Assembly);
```

See the [configuration settings documentation](https://fast-endpoints.com/docs/configuration-settings#excluding-assemblies-from-scanning) for details.

</details>

## Fixes 🪲

<details><summary>Permission codes could depend on the machine's culture</summary>

The 3-character codes generated for permissions were upper-cased with the current culture. On a machine with a Turkish culture an `i` became `İ`, so a code could contain a non-ASCII letter, and the codes generated at build time by the source generator could differ from the ones computed at runtime if the two machines used different cultures. Both now use invariant casing, so the codes are always `A-Z0-9`.

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