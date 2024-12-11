---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Control binding sources per DTO property</summary>

The default binding order is designed to minimize attribute clutter on DTO models. In most cases, disabling binding sources is unnecessary. However, for rare scenarios where a binding source must be explicitly blocked, you can do the following:

```cs
[DontBind(Source.QueryParam | Source.RouteParam)] 
public string UserID { get; set; } 
```

The opposite approach can be taken as well, by just specifying a single binding source for a property like so:

```cs
[FormField]
public string UserID { get; set; }

[QueryParam]
public string UserName { get; set; }

[RouteParam]
public string InvoiceID { get; set; }
```

</details>

## Improvements 🚀

## Fixes 🪲

<details><summary>Incorrect latest version detection with 'Release Versioning' strategy</summary>

The new release versioning strategy was not correctly detecting the latest version of an endpoint if there was multiple endpoints for the same route such as a GET & DELETE endpoint on the same route.

</details>

<details><summary>Issue with nullability context not being thread safe in Swagger processor</summary>

In rare occasions where swagger documents were being generated concurrently, an exception was being thrown due to `NullabilityInfoContext` not being thread safe. 
This has been fixed by implementing a caching mechanism per property type.

</details>

## Minor Breaking Changes ⚠️