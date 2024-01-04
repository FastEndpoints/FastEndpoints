---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

[//]: # (## New 🎉)

[//]: # (## Improvements 🚀)

[//]: # (## Fixes 🪲)

## Breaking Changes ⚠️

<details><summary>'SendRedirectAsync()' method signature change</summary>

The method signature has been updated to the following:

```csharp
SendRedirectAsync(string location, bool isPermanent = false, bool allowRemoteRedirects = false)
```

This would be a breaking only if you were doing any of the following:

- Redirecting to a remote url instead of a local url. In which case simply set `allowRemoteRedirects` to `true`. otherwise the new behavior will throw an exception.
  this change was done to prevent [open redirect attacks](https://learn.microsoft.com/en-us/aspnet/mvc/overview/security/preventing-open-redirection-attacks) by default.

- A cancellation token was passed in to the method. The new method does not support cancellation due to the underlying `Results.Redirect(...)` methods do not support
  cancellation.

</details>