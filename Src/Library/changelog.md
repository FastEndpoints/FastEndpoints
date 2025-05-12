---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉


## Improvements 🚀


## Fixes 🪲


## Minor Breaking Changes ⚠️

<details><summary>Behavior change with multiple binding sources and the 'required' keyword</summary>

If a request DTO has required properties like so:

```cs
{
    public required string UserId { get; set; } //to be bound from route param
    public required string Name { get; set; } //to be bound from json body
}
```

The previous advice was to simply decorate the `UserId` property with a `[JsonIgnore]` attribute so that the serializer will ignore the `required` keyword and won't complain due to missing data for that property in the incoming JSON body.

Even though the `[JsonIgnore]` attribute seemed logical for this purpose at the time, we've come to realize it has the potential to cause problems elsewhere.

So, if you are using the `required` keyword on DTO properties that are to be bound from a non-json binding source such as route/query params, form fields, headers, claims, etc. and would like to keep on using the `required` keyword (even though it doesn't really make much sense in the context of request DTOs in most cases), you should remove the `[JsonIgnore]` property and annotate the binding related attribute that actually specifies what binding source should be used for that property, such as `[RouteParam]`, `[QueryParam]`, `[FormField]`, `[FromClaim]`, `[FromHeader]`, etc.

The request DTO now needs to look like the following:

```cs
sealed class MyRequest
{
    [RouteParam]
    public required string UserId { get; set; }

    public required string Name { get; set; }
}
```

</details>
