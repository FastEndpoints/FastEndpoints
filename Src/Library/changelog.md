
---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Source generated access control lists</summary>

Todo: update doc page and link from here.

</details>

## Improvements 🚀

<details><summary>Ability to get rid of null-forgiving operator '!' from test code</summary>

The `TestResult<TResponse>.Result` property is no longer a nullable property. This change enables us to get rid of the null-forgiving operator `!` from our integration test code.
Existing test code wouldn't have to change. You just don't need to use the `!` to hide the compiler warnings anymore. If/when the value of the property is actually `null`, the tests will 
just fail with a NRE, which is fine in the context of test code.

</details>

<details><summary>Allow customizing serialization/deserialization of Event/Command objects in Job/Event Queue storage</summary>

Todo: update doc page and link from here.
Ref: https://github.com/FastEndpoints/FastEndpoints/issues/480

</details>

<details><summary>Optimize source generator performance</summary>

The type discovery generator is now highly efficient and only generates the source when any of the target types changes or new ones are added.

</details>

<details><summary>Optimize startup routine</summary>

Authorization policy building is moved to the `MapFastEndpoints` stage avoiding the need to iterate the discovered endpoint collection twice. This also avoids any potential race conditions due to different middleware pipeline config/ordering edge cases.

</details>

## Fixes 🪲

<details><summary>Startup issue due to 'IAuthorizationService' injection</summary>

v5.16 had introduced a bug of not being able to inject `IAuthorizationService` into endpoint classes, which has now been fixed.

</details>

<details><summary>Startup type discovery issue with exclusion list</summary>

Since you can override the exclusion list by doing:

```cs
.AddFastEndpoints(o.Assemblies = new[] { typeof(SomeClass).Assembly });
```

This was not working if the assembly name didn't have a dot (.) in the namespace.  

</details>

<!-- ## Breaking Changes ⚠️ -->