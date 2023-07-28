
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- ### 📢 New -->

### 🚀 Improvements

<details><summary>1️⃣ Allow customization of in-memory event queue size</summary>

If you're are using the [default in-memory event storage providers](https://fast-endpoints.com/docs/remote-procedure-calls#event-bus-vs-event-queue), the size limit of their internal queues can now be specified like so:

```cs
InMemoryEventQueue.MaxLimit = 1000;
```
This limit is applied per queue. Each event type in the system has it's own independent queue. If there's 10 events in the system,
there will be 10X the number of events held in memory if they aren't being dequeued in a timely manner.

</details>

<details><summary>2️⃣ Remote messaging performance improvements</summary>

- Refactor logging to use code generated high performance logging.
- Reduce allocations for `void` commands by utilizing a static `EmptyObject` instance.

</details>

<details><summary>3️⃣ Event Queues internal optimizations</summary>

- Use `SemaphoreSlim`s instead of `Task.Delay(...)` for message pump

</details>

<details><summary>3️⃣ Misc. performance improvements</summary>

- Reduce boxing/unboxing in a few hot paths.

</details>

<!-- ### 🪲 Fixes -->

### ⚠️ Minor Breaking Changes

<details><summary>1️⃣ Event Queue storage provider API changes</summary>

There has been several implementation changes to the custom storage providers to provide a more user-friendly experience. Please see the updated [doc page](https://fast-endpoints.com/docs/remote-procedure-calls#reliable-event-queues-with-persistence) for the current usage.

</details>

<!-- <details><summary>1️⃣ some title</summary></details> -->
