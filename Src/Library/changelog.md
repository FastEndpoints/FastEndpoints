
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

### ⚠️ Minor Breaking Changes

<details><summary>1️⃣ RPC remote connection configuration method renamed</summary>

Due to the introduction of remote Pub/Sub messaging (see new features below), it no longer made sense to call the method `MapRemoteHandlers` as it now supports both remote handlers and event hubs.

```cs
app.MapRemoteHandlers(...) -> app.MapRemote(...)
```
</details>


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

<details><summary>2️⃣ Remote Pub/Sub Event Queues</summary>

Please refer to the [documentation](https://fast-endpoints.com/docs/remote-procedure-calls#remote-pub-sub-event-queues) for details of this feature.

</details>

<!-- ### 🚀 Improvements -->

### 🪲 Fixes

<details><summary>Scope creation in a Validator was throwing an exception in unit tests</summary>

Validator code such as the following was preventing the validator from being unit tested via the `Factory.CreateValidator<T>()` method, which has now been fixed.

```cs
public class IdValidator : Validator<RequestDto>
{
    public IdValidator()
    {
        using var scope = CreateScope();
        var idChecker = scope.Resolve<IdValidationService>();

        RuleFor(x => x.Id).Must((id)
            => idChecker.IsValidId(id));
    }
}
```

</details>