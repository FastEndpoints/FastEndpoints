
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

<!-- ### 🪲 Fixes -->

<!-- ### ⚠️ Minor Breaking Changes -->

<!-- <details><summary>1️⃣ some title</summary></details> -->
