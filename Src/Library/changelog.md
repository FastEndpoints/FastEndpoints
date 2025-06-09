---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Automatic 'Accepts Metadata' for Non-Json requests</summary>

In the past, if an endpoint defines a request DTO type, an accepts-metadata of `application/json` would be automatically added to the endpoint, which would require the user to [clear that default metadata](https://fast-endpoints.com/docs/swagger-support#clearing-only-accepts-metadata) if all of the properties of the DTO is bound from non-json binding sources such as route/query/header etc.

Now, if the user annotates all the properties of a DTO with the respective non-json binding sources such as the following:

```cs
sealed class GetAccountStatementRequest
{
    [RouteParam]
    public int UserId { get; set; }
    
    [QueryParam]
    public DateTime DateFrom { get; set; }

    [QueryParam]
    public DateTime DateTo { get; set; }
}
```

It is no longer necessary for the user to manually clear the default accepts-metadata as the presence of non-json binding source attributes on each of the DTO properties allows us to correctly detect that there's not going to be any JSON body present in the incoming request.

</details>

<details><summary>Support for 'DataAnnotations' Validation Attributes</summary>

You can now annotate request DTO properties with `DataAnnotations` attributes such as `[Required], [StringLength(...)]` etc., instead of writing a `FluentValidations` validator for quick-n-dirty input validation. Do note however, only one of the strategies can be used for a single endpoint. I.e. if a request DTO has annotations as well as a fluent validator, only the fluent validator will be run and the annotations will be ignored. Mixing strategies is not allowed in order to prevent confusion for the reader. To enable `DataAnnotations` support, please enable the setting like so:

```cs
app.UseFastEndpoints(c => c.Validation.EnableDataAnnotationsSupport = true)
```

</details>

## Improvements 🚀

## Fixes 🪲

<details><summary>Swagger generation bug caused by lists of request DTOs</summary>

A new feature introduced in `v6.1` caused swagger generation to fail if the request DTO type of the endpoint is a `List<T>`, which has been corrected.

</details>

<details><summary>Infinite recursion issue with swagger generation due to self referencing validators</summary>

If a request uses a self referencing validator for nested properties, a stack overflow was happening due to infinite recursion.

</details>

<details><summary>Issue with generic post-processor registration</summary>

Generic post-processors were not being correctly registered due to an oversight, which has been corrected with this release.

</details>

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