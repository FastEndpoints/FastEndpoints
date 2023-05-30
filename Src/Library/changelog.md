
---
### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---
### ⚠️ Breaking Changes

---
### 📢 New

---
### 🚀 Improvements
1️⃣ - New extension methods to make it easier to add `Roles` and `Permissions` with `params` and with tuples for `Claims` when creating JWT tokens.
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
##
2️⃣ - The unit testing `Factory.Create<T>(...)` method will now inform which service you forgot to register if either the endpoint or one of the dependencies requires a service. 
In which case, you'd be registering that service like below:
```cs
var ep = Factory.Create<Endpoint>(ctx =>
{
    ctx.AddTestServices(s => s.AddScoped<ScopedSvc>());
});
```

---
### 🪲 Fixes
