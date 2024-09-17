---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Job Queuing support for Commands that return a result</summary>

todo: update docs + describe here

</details>

## Improvements 🚀

<details><summary>Make Pre/Post Processor Context's 'Request' property nullable</summary>

Since there are certain edge cases where the `Request` property can be `null` such as when STJ receives invalid JSON input from the client and fails to successfully deserialize the content. Even in those cases, pre/post processors would be executed where the pre/post processor context's `Request` property would be null. This change would allow the compiler to remind you to check for null if the `Request` property is accessed from pre/post processors.

</details>

## Fixes 🪲

## Minor Breaking Changes ⚠️