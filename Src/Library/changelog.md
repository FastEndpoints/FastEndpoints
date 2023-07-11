
---

### ⭐ Looking For Sponsors
FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

### 📢 New

<details><summary>1️⃣ gRPC based Event Broker functionality</summary>

Please see the documentation [here](https://fast-endpoints.com/docs/remote-procedure-calls#event-broker-mode) for details.

</details>

<details><summary>2️⃣ Ability to subscribe to exceptions in Event Queues</summary>

Please see the documentation [here](https://fast-endpoints.com/docs/remote-procedure-calls#event-queue-error-notifications) for details.

</details>

<details><summary>3️⃣ Support for TimeProvider in FastEndpoints.Security package</summary>

You can now register your own [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider) implementation in the IOC container and the `FastEndpoints.Security` package will use that implementation to obtain the current time for token creation. If no `TimeProvider` is registered, the `TimeProvider.System` default implementation is used. There's no need to wait for .NET 8.0 release since the `TimeProvider` abstract class is already in a `netstandard2.0` BCL package on nuget. #458

</details>

<details><summary>4️⃣ Support for Asp.Versioning.Http package</summary>

todo: write doc page and put link to it here.

</details>

### 🚀 Improvements

<details><summary>1️⃣ Optimize Event Queue internals</summary></details>
<details><summary>2️⃣ Upgrade dependencies to latest</summary></details>

<!-- ### 🪲 Fixes -->

<!-- ### ⚠️ Minor Breaking Changes -->
