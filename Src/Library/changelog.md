---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

## Improvements 🚀

<details><summary>Ability to customize param names for strongly typed route params</summary>

It is now possible to customize the route param names when using the [strongly typed route params](https://fast-endpoints.com/docs/misc-conveniences#strongly-typed-route-parameters) feature by simply decorating the target dto property with a `[BindFrom("customName"))]` attribute. If a `BindFrom` attribute annotation is not present on the property, the actual name of the property itself will end up being the route param name.

</details>

## Fixes 🪲

<details><summary>Header example value not picked up from swagger example request</summary>

If a request DTO specifies a custom header name that is different from the property name such as the following:

```cs
sealed class GetItemRequest
{
    [FromHeader("x-correlation-id")]
    public Guid CorrelationId { get; init; }
}
```

and a summary example request is provided such as the following:

```cs
Summary(s => s.ExampleRequest = new GetItemRequest()
{
    CorrelationId = "54321"
});
```

the example value from the summary example property was not being picked up due to an oversight.

</details>

## Breaking Changes ⚠️