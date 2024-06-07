---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

## Improvements 🚀

<details><summary>Change default redirection behavior of cookie authentication middleware</summary>

The default behavior of the ASP.NET cookie auth middleware is to automatically return a redirect response when current user is either not authenticated or unauthorized. This default behavior is not appropriate for REST APIs because there's typically no login UI page as part of the backend server to redirect to, which results in a `404 - Not Found` error which confuses people that's not familiar with the cookie auth middleware. The default behavior has now been overridden to correctly return a `401 - Unauthorized` & `403 - Forbidden` as necessary without any effort from the developer.

</details>

## Fixes 🪲

<details><summary>[HideFromDocs] attribute missing issue with the source generator</summary>

If the consuming project didn't have a `global using FastEndpoints;` statement, the generated classes would complain about not being able to located the said attribute, which has now been rectified.

</details>

## Minor Breaking Changes ⚠️