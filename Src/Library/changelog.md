---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

- customize error response content-type globally (https://github.com/FastEndpoints/FastEndpoints/issues/627)
- `DontAutoSend()` support for `Results<T1,T2,...>` returning handlers ()
- `ProblemDetails` title transformer (https://github.com/FastEndpoints/FastEndpoints/issues/637)
- allow usage of empty request dtos (https://github.com/FastEndpoints/FastEndpoints/issues/641)
- ability to specify output file name for `ExportSwaggerJsonAndExitAsync()`

## Improvements 🚀

- `PreSetupAsync()` for `AppFixture` to allow async work that contributes to WAF creation. (https://discord.com/channels/933662816458645504/1214837140983123979/1215538377521365053)
- automatically rewind request body with `IPlainTextRequest` if `EnableBuffering()` is used. (https://github.com/FastEndpoints/FastEndpoints/issues/631)
- filter out illegal header names from being created as request parameters in swagger. (https://github.com/FastEndpoints/FastEndpoints/issues/615)
- implement `[FromBody]` attribute support for routeless integration testing. (https://github.com/FastEndpoints/FastEndpoints/issues/645)
- hydrate integration testing route url with values from request dto. (https://github.com/FastEndpoints/FastEndpoints/pull/648)
- upgrade dependencies to latest

## Fixes 🪲

- duplicate swagger tag descriptions issue (https://github.com/FastEndpoints/FastEndpoints/issues/654)

[//]: # (## Breaking Changes ⚠️)