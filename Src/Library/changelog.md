
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

### 🔖 New

<details><summary>Source generated access control lists</summary>

Todo: update doc page and link from here.

</details>

### 🚀 Improvements

<details><summary>Ability to get rid of null-forgiving operator '!' from test code</summary>

The `TestResult<TResponse>.Result` property is no longer a nullable property. This change enables us to get rid of the null-forgiving operator `!` from our integration test code.
Existing test code wouldn't have to change. You just don't need to use the `!` to hide the compiler warnings anymore. If/when the value of the property is actually `null`, the tests will 
just fail with a NRE, which is fine in the context of test code.

</details>

<details><summary>Allow customizing serialization/deserialization of Event/Command objects in Job/Event Queue storage</summary>

Todo: update doc page and link from here.

</details>

<!-- ### 🪲 Fixes -->

<!-- ### ⚠️ Minor Breaking Changes -->
