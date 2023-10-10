---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

<!-- ## New 🎉 -->

## Improvements 🚀

<details><summary>Prevent swallowing of STJ exceptions in edge cases</summary>

If STJ throws internally after it has started writing to the response stream, those exceptions will no longer be swallowed.
This can happen in rare cases such as when the DTO being serialized has an infinite recursion depth issue.

</details>

<details><summary>Misc. improvements</summary>

- Upgrade dependencies to latest

</details>

<!-- ## Fixes 🪲 -->


<!-- ## Minor Breaking Change ⚠️ -->