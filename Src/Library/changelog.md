---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

[//]: # (## New 🎉)

## Improvements 🚀

<details><summary>Micro optimization with 'Concurrent Dictionary' usage</summary></details>

Concurrent dictionary `GetOrAdd()` overload with lambda parameter seems to perform a bit better in .NET 8. All locations that were using the other overload was
changed to use the overload with the lambda.

[//]: # (## Fixes 🪲)

[//]: # (## Breaking Changes ⚠️)