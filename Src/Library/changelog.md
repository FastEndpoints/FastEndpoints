
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- ### ⚠️ Breaking Changes -->

### 📢 New

<details>
<summary>1️⃣ GRPC based Remote-Procedure-Calls</summary>

Please refer to the [documentation](https://fast-endpoints.com/docs/command-bus#dependency-injection) for details of this feature.
</details>

<details>
<summary>2️⃣ Unit test Endpoints that use Resolve&lt;T&gt;() methods</summary> 

It's now possible to unit test endpoints (including dependencies) that use the `Resolve<T>()` methods to resolve services from DI. This is especially helpful when resolving `Scoped` services in `Mapper` classes. Just register the services that need to be "Resolved" like so:

```cs
var ep = Factory.Create<Endpoint>(ctx =>
{
    ctx.AddTestServices(s => s.AddTransient<MyService>());
});
```
An example mapper that uses the `Resolve<T>()` method would be such as this:

```cs
public class Mapper : Mapper<Request, Response, Entity>
{
    public override Entity ToEntity(Request r)
    {
        var mySvc = Resolve<MyService>();
    }
}
```
</details>

<details>
<summary>3️⃣ Unit test Mapper & Validator classes that use Resolve&lt;T&gt;()</summary>

Mappers & Validators that use the `Resolve<T>()` methods to obtain services from the DI container can now be unit tested by supplying the necessary dependencies.

```cs
var validator = Factory.CreateValidator<AgeValidator>(s =>
{
    s.AddTransient<AgeService>();
});
```

Use `Factory.CreateMapper<TMapper>()` the same way in order to get a testable instance of a mapper.
</details>

<details>
<summary>4️⃣ Overloads for adding Claims, Roles & Permissions when creating JWT tokens</summary>

New extension method overloads have been added to make it easier to add `Roles` and `Permissions` with `params` and with tuples for `Claims` when creating JWT tokens.

```cs
var jwtToken = JWTBearer.CreateToken(
    priviledges: u =>
    {
        u.Roles.Add(
            "Manager",
            "Employee");
        u.Permissions.Add(
            "ManageUsers",
            "ManageInventory");
        u.Claims.Add(
            ("UserName", req.Username),
            ("Email", req.Email));
    });
```
</details>

### 🚀 Improvements

<details>
<summary>1️⃣ Alert which service was not registered when unit testing</summary>

The unit testing `Factory.Create<T>(...)` method will now inform which service you forgot to register if either the endpoint or one of the dependencies requires a service. In which case, you'd be registering that service like below:

```cs
var ep = Factory.Create<Endpoint>(ctx =>
{
    ctx.AddTestServices(s => s.AddScoped<ScopedSvc>());
});
```

</details>

<!-- ### 🪲 Fixes -->
