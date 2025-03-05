---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Bypass endpoint caching for integration tests</summary>

You can now easily test endpoints that have caching enabled, by using a client configured to automatically bypass caching like so:

```cs
var antiCacheClient = App.CreateClient(new() { BypassCaching = true });
```

</details>

<details><summary>Mark properties as "bind required"</summary>

You can now make the request binder automatically add a validation failure when binding from route params, query params, and form fields by decorating the dto properties if the binding source doesn't provide a value:

```cs
sealed class MyRequest
{
    [QueryParam(IsRequired = true)]
    public bool Correct { get; set; }

    [RouteParam(IsRequired = true)]
    public int Count { get; set; }

    [FormField(IsRequired = true)]
    public Guid Id { get; set; }
}
```

</details>

<details><summary>Generic command support for job queues</summary>

Closed generic commands can now be registered like so:

```cs
app.Services.RegisterGenericCommand<QueueCommand<OrderCreatedEvent>, QueueCommandHandler<OrderCreatedEvent>>();
```

and then be queued as jobs like so:

```cs
await new QueueEventCommand<OrderCreatedEvent>()
{ 
  ...
}.QueueJobAsync();
```

Note: Open generic commands are not supported for job queueing.

</details>

<details><summary>Inter-Process-Communication via Unix-Domain-Sockets</summary>

The [FastEndpoints.Messaging.Remote](https://fast-endpoints.com/docs/remote-procedure-calls) library can now do inter-process-communication via unix sockets when everything is running on the same machine by doing the following:

```cs
//server setup
bld.WebHost.ConfigureKestrel(k => k.ListenInterProcess("ORDERS_MICRO_SERVICE"));

//client setup
app.MapRemote("ORDERS_MICRO_SERVICE", c => c.Register<CreateOrderCommand>());
```

When a service is lifted out to a remote machine, all that needs to be done is to update the connection settings like so:

```cs
//server
bld.WebHost.ConfigureKestrel(k => k.ListenAnyIP(80, o => o.Protocols = HttpProtocols.Http2);

//client
app.MapRemote("http://orders.my-app.com", c => c.Register<CreateOrderCommand>());
```

</details>

<details><summary>Optionally save command type on job storage record</summary>

A new optional/addon interface `IHasCommandType` has been introduced if you need to persist the full type name of the command that is associated with the job storage record. Simply implement the new interface on your job storage record and the system will automatically populate the property value before being persisted.

</details>

## Improvements 🚀

<details><summary>RPC Event Subscriber parallel execution</summary>

Event subscribers used to execute the event handlers in sequence when a batch of event storage records were fetched from the storage provider.
The handlers will now be executed in parallel just like how parallel execution happens in job queues.

</details>

<details><summary>RPC Event Hub startup sequence</summary>

The rpc event hub was using a thread sleep pattern during startup to restore subscriber IDs via the storage provider, resulting in a sequential initialization.
It has been refactored to use an IHostedService together with retry logic for a proper async and parallel initialization, resulting in decreased startup time.

</details>

<details><summary>RPC Event Hub event distribution</summary>

Previously if a hub was not registered before events were broadcasted, or if event serialization fails due to user error, those exceptions would have been swallowed in some cases.
The internals of the hub has been refactored to surface those exceptions when appropriate.

</details>

<details><summary>Workaround for NSwag quirk with byte array responses</summary>

NSwag has a quirk that it will render an incorrect schema if the user does something like the following:

```cs
b => b.Produces<byte[]>(200, "image/png");
```

In order to get the correct schema generated, we've had to do the following:

```cs
b => b.Produces<IFormFile>(200, "image/png");
```

You now have the ability to do either of the above, and it will generate the correct schema.

</details>

## Fixes 🪲

<details><summary>Job result is null when job execution failure occurs</summary>

The result property of job records that was passed into the `OnHandlerExecutionFailureAsync()` method of the storage provider was `null` due to an oversight, which has been corrected.

</details>

<details><summary>Parallel 'VersionSet' creation issue</summary>

If `VersionSet`s are created by multiple SUTs at the same time when doing integration testing, a non-concurrent dictionary modification exception was being thrown.
The internal dictionary used to keep track of the version sets has been changed to a concurrent dictionary which solves the issue.

</details>

## Breaking Changes (Minor) ⚠️

<details><summary>'IEventHubStorageProvider' contract change</summary>

In order to improve database write performance, the `IEventHubStorageProvider.StoreEventAsync(TStorageRecord r, CancellationToken ct)` method signature has been changed to the following:

```cs
ValueTask StoreEventsAsync(IEnumerable<TStorageRecord> r, CancellationToken ct);
```

Previously, records were persisted one at a time. Now, the records are supplied in batches allowing you to take advantage of batched inserts and/or transactions improving database write performance and consistency.

NOTE: You should make sure either none or all of the supplied records are persisted to disk in order to avoid duplicate events being published due to the built-in retry mechanism.

</details>

<details><summary>'IEvent.Broadcast()' extension method is no longer cancellable</summary>

The `BroadCast()` method is now a fire-n-forget method and no longer accepts a `CancellationToken`, which simplifies event publication.

</details>