
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

### 📢 New

<details><summary>1️⃣ Support for TimeProvider in FastEndpoints.Security package</summary>

You can now register your own [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider) implementation in the IOC container and the `FastEndpoints.Security` package will use that implementation to obtain the current time for token creation. If no `TimeProvider` is registered, the `TimeProvider.System` default implementation is used. There's no need to wait for .NET 8.0 release since the `TimeProvider` abstract class is already in a `netstandard2.0` BCL package on nuget. #458

</details>

<!-- ### 🚀 Improvements -->

<!-- ### 🪲 Fixes -->

<!-- ### ⚠️ Minor Breaking Changes -->
