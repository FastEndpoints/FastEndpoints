
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## 🔖 New

<details><summary>Integration testing with fake/test handlers for messaging features</summary>

Both `In-Proc` and `RPC` based messaging functionality can now be easily integration tested by registering fake/test handlers during testing. See below links for examples of each:

- [Command Bus](https://fast-endpoints.com/docs/command-bus) ([example](https://github.com/FastEndpoints/FastEndpoints/blob/fcb18db8e938fc850ea517d298ecaadd869d0f7c/Tests/IntegrationTests/FastEndpoints/CommandBusTests/CommandBusTests.cs#L73-L84))
- [Job Queues](https://fast-endpoints.com/docs/job-queues#queueing-a-job) ([example](https://github.com/FastEndpoints/Job-Queue-Demo/tree/main/Test))
- [Event Bus](https://fast-endpoints.com/docs/event-bus) ([example](https://github.com/FastEndpoints/FastEndpoints/blob/fcb18db8e938fc850ea517d298ecaadd869d0f7c/Tests/IntegrationTests/FastEndpoints/EventBusTests/EventBusTests.cs#L22-L35))
- [Event-Queue/Broker](https://fast-endpoints.com/docs/remote-procedure-calls#remote-pub-sub-event-queues) ([example](https://github.com/FastEndpoints/Event-Broker-Demo/tree/main/Test))

</details>

<details><summary>[DontRegister] attribute for skipping auto registration</summary>

Any auto discovered types (endpoints/commands/events/etc.) can be annotated with the attribute `[DontRegister]` if you'd like it to be skipped while assembly scanning for auto registration.

</details>

<details><summary>Auto instantiation of 'JsonSerializerContext' with global 'SerializerOptions'</summary>

```cs
public override void Configure()
{
    ...
    SerializerContext<UpdateAddressCtx>();
}
```

By specifying just the type of the serializer context, instead of supplying an instance as with the existing method, the context will be created using the `SerializerOptions` that you've configured at startup using the `UseFastEndpoints(...)` call.

</details>

## 🚀 Improvements

<details><summary>Minor performance optimizations</summary>

- Job queue message pump improvements

</details>

<details><summary>Concurrent WAF testing</summary>

- Better thread safety of `EndpointData` when running concurrent integration tests
- Avoid potential contention issues for `Event Handlers` when integration testing

</details>

## 🪲 Fixes

<details><summary>Event handler constructors being called twice</summary>

Due to an oversight in `IEnumerable` iteration, the event handler constructor was being called twice per execution. Thank you [Wahid Bitar](https://github.com/WahidBitar) for reporting it.

</details>

<!-- ## ⚠️ Minor Breaking Changes -->