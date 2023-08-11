
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## 📢 New

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

<details><summary>Support for integration testing Event Queues/Brokers</summary>

gRPC based [Event Queues](https://fast-endpoints.com/docs/remote-procedure-calls#remote-pub-sub-event-queues) can now be integration tested by supplying fake event handlers as shown [here](https://github.com/FastEndpoints/Event-Broker-Demo/tree/main/Test).

</details>

<details><summary>Support integration testing Job Queues</summary>

[Job Queues](https://fast-endpoints.com/docs/job-queues#queueing-a-job) can now be integration tested by supplying fake command handlers as shown [here](https://github.com/FastEndpoints/Job-Queue-Demo/tree/main/Test).

</details>

<details><summary>[DontRegister] attribute for skipping auto registration</summary>

Any auto discovered types (endpoints/commands/events/etc.) can be annotated with the attribute `[DontRegister]` if you'd like it to be skipped while assembly scanning for auto registration.

</details>

## 🚀 Improvements

<details><summary>Minor performance optimizations</summary>

- Job queue message pump improvements

</details>

<details><summary>Concurrent WAF testing</summary>

- Better thread safety of `EndpointData` when running concurrent integration tests

</details>

## 🪲 Fixes

<details><summary>Event handler constructors being called twice</summary>

Due to an oversight in `IEnumerable` iteration, just the event handler constructor was being called twice per execution. Thank you [Wahid Bitar](https://github.com/WahidBitar) for reporting it.

</details>

<!-- ## ⚠️ Minor Breaking Changes -->