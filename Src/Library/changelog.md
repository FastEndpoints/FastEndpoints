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

<details><summary>Support for malformed JSON array string binding</summary>

When submitting requests via SwaggerUI where a complex object collection is to be bound to a collection property of a DTO, SwaggerUI sends in a malformed string of JSON objects without properly enclosing them in the JSON array notation `[...]` such as the following:

```json
{"something":"one"},{"something":"two"}
```

whereas it should be a proper JSON array such as this:

```json
[{"something":"one"},{"something":"two"}]
```

Since we have no control over how SwaggerUI behaves, support has been added to the default request binder to support parsing and binding the malformed comma separateed JSON objects that SwaggerUI sends at the expense of a minor performance hit.

</details>

<details><summary>Auto infer query parameters for routeless integration tests</summary>

If you annotate request dto properties with `[RouteParam]` attribute, the helper extensions such as `.GETAsync()` will now automatically populate
the request query string with values from the supplied dto instance when sending integration tests.

```cs
sealed class MyRequest
{
    [RouteParam]
    public string FirstName { get; set; }

    public string LastName { get; set; }
}

[Fact]
public async Task Query_Param_Test()
{
    var request = new MyRequest
    {
        FirstName = "John", //will turn into a query parameter
        LastName = "Gallow" //will be in json body content
    };
    var result = await App.Client.GETAsync<MyEndpoint, MyRequest, string>(request);
}
```

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

<details><summary>Xml comments not picked up by swagger for request schema</summary>

There was a regression in the code path that was picking up `Summary` xml comments from DTO properties in certain scenarios, which has now been fixed.

</details>

<details><summary>Incorrect swagger example generation when '[FromBody] or [FromForm]' was used</summary>

If a request DTO was defined like this:

```cs
sealed class MyRequest
{
    [FromBody]
    public Something Body { get; set; }
}
```

and an example request is provided via the Summary like this:

```cs
Summary(x=>x.ExampleRequest = new MyRequest()
{
    Body = new Something()
    {
        ...
    }
});
```

swagger generated the incorrect request example value which included the property name, which it shouldn't have.

</details>

<details><summary>Default exception handler setting incorrect mime type</summary>

Due to an oversight, the [default exception handler](https://fast-endpoints.com/docs/exception-handler) was not correctly setting the intended content-type value of `application/problem+json`. Instead, it was being overwritten with `application/json` due to not using the correct overload of `WriteAsJsonAsync()` method internally.

</details>

## Breaking Changes ⚠️