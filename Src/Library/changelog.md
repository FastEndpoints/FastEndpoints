---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Multi-level test-collection ordering</summary>

Tests can now be ordered by prioritizing test-collections, test-classes in those collections as well as tests within the classes for fully controlling the order of test execution when test-collections are involved. [See here](https://fast-endpoints.com/docs/integration-unit-testing#ordering-tests-in-collections) for a usage example.

</details>

<details><summary>Customize character encoding of JSON responses</summary>

A new config setting has been added to be able to customize the charset of JSON responses. `utf-8` is used by default. can be set to `null` for disabling the automatic appending of the charset to the `Content-Type` header of responses.

```csharp
app.UseFastEndpoints(c => c.Serializer.CharacterEncoding = "utf-8")
```

</details>

## Improvements 🚀

<details><summary>Remove dependency on 'Xunit.Priority' package</summary>

The 'Xunit.Priority' package is no longer necessary as we've implemented our own test-case-orderer. If you've been using test ordering with the `[Priority(n)]` attribute, all you need to do is get rid of any `using` statements that refer to `XUnit.Priority`.

</details>

## Fixes 🪲

## Minor Breaking Changes ⚠️