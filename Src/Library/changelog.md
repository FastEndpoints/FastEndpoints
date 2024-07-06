---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

## Improvements 🚀

## Fixes 🪲

<details><summary>Swagger generator issue with [FromBody] properties</summary>

The referenced schema was generated as a `OneOf` instead of a direct `$ref` when a request DTO property was being annotated with the `[FromBody]` attribute.

</details>

<details><summary>Kiota client generation issue with 'clean output' setting</summary>

If the setting for cleaning the output folder was enabled, Kiota client generation was throwing an error that it can't find the input swagger json file, because Kiota deletes everything in the output folder when that setting is enabled. From now on, if the setting is enabled, the swagger json file will be created in the system temp folder instead of the output folder.

</details>

## Minor Breaking Changes ⚠️