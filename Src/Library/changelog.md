---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Customize character encoding of JSON responses</summary>

A new config setting has been added to be able to customize the charset of JSON responses. `utf-8` is used by default. can be set to `null` for disabling the automatic appending of the charset to the `Content-Type` header of responses.

```csharp
app.UseFastEndpoints(c => c.Serializer.CharacterEncoding = "utf-8")
```

</details>

## Improvements 🚀

## Fixes 🪲

## Minor Breaking Changes ⚠️