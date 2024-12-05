---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Control binding sources per DTO property</summary>

You can annotate a request DTO property with the newly added `[DontBind(...)]` attribute and specify which binding sources should not be used for that particular property when model binding.

```cs
sealed class GetUserRequest
{
    [DontBind(Source.QueryParam | Source.RouteParam)]
    public string UserId { get; set; }
}
```

Doing this is not necessary in 99% of cases and, should be reserved for specific requirements where you need to explicitly ban a certain binding source for a property. 
The point of the default behavior is to reduce the attribute noise on DTO models. Doing this for all properties sort of defeats that purpose. Use with care!

</details>

## Improvements 🚀

## Fixes 🪲

<details><summary>Incorrect latest version detection with 'Release Versioning' strategy</summary>

The new release versioning strategy was not correctly detecting the latest version of an endpoint if there was multiple endpoints for the same route such as a GET & DELETE endpoint on the same route.

</details>

## Minor Breaking Changes ⚠️