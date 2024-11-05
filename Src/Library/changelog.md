---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Support for enforcing antiforgery token checks for non-form requests</summary>

The antiforgery middleware can now be configured to check antiforgery tokens for any content-type by configuring it like so:

```csharp
app.UseAntiforgeryFE(additionalContentTypes: ["application/json"])
```

</details>

## Improvements 🚀

## Fixes 🪲

<details><summary>Struct support for request DTOs</summary>

Adding the new reflection source generator broke support for struct types to be used for request DTOs, which has been corrected in this release.

</details>

<details><summary>Struct support for Reflection Source Generator</summary>

The reflection generator was not generating the correct source for unboxing value types.

</details>

## Minor Breaking Changes ⚠️