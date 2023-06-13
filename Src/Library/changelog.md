
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- ### ⚠️ Breaking Changes -->

### 📢 New

<details><summary>1️⃣ Support for RPC Commands that do not return any result</summary>

Remote procedure calls via `ICommand` & `ICommandHandler<TCommand>` is now possible which the initial RPC feature did not support. Command/Handler registration is done the same way:

```cs
//SERVER
app.MapHandlers(h =>
{
    h.Register<SayHelloCommand, SayHelloHandler>();
});

//CLIENT
```cs
app.MapRemoteHandlers("http://localhost:6000", c =>
{
    c.Register<SayHelloCommand>();
});

//COMMAND EXECUTION
await new SayHelloCommand { From = "mars" }.RemoteExecuteAsync();
```
</details>

<!-- ### 🚀 Improvements -->

<!-- ### 🪲 Fixes -->
