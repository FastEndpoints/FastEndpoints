
---
### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---
### ⚠️ Breaking Changes

---
### 📢 New

---
### 🚀 Improvements

New extension methods to make it easier to add `Roles` and `Permissions` with `params` and with tuples for `Claims` when creating JWT tokens.
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

---
### 🪲 Fixes
