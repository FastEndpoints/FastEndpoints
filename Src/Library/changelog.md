---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Convenience method for retrieving the auto generated endpoint name</summary>

You can now obtain the generated endpoint name like below for the purpose of custom link generation using the `LinkGenerator` class.

```cs
var endpointName = IEndpoint.GetName<SomeEndpoint>();
```

</details>

<details><summary>Auto population of headers in routeless tests</summary>

Given a request dto such as the following where a property is decorated with the `[FromHeader]` attribute:

```cs
sealed class Request
{
    [FromHeader]
    public string Title { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

Previously, you had to manually add the header to the request in order for the endpoint to succeed without sending back an error response.
Now you can simply supply the value for the header when making the request as follows, and the header will be automatically added to the request with the value from the property.

```cs
var (rsp, res) = await App.Client.POSTAsync<MyEndpoint, Request, string>(
                     new()
                     {
                         Title = "Mrs.",
                         FirstName = "Doubt",
                         LastName = "Fire"
                     });
```

This automatic behavior can be disabled as follows if you'd like to keep the previous behavior:

```cs
POSTAsync<...>(..., populateHeaders: false);
```

</details>

<details><summary>Comma separated value binding support for collection properties</summary>

It was only possible to model bind collection properties if the values were submitted either as json arrays or duplicate keys.
You can now submit csv data for automatically binding to collection properties such as the following:

```cs
public class FindRequest
{
    [QueryParam]
    public string[] Status { get; set; }
}
```

A url with query parameters such as this would work out-of-the-box now:

```ini
/find?status=queued,completed
```

</details>


<details><summary>'SendAcceptedAtAsync()' method for endpoints</summary>

The following method is now available for sending a `202 - Accepted` similarly to the `201 - Created` response.

```cs
await SendCreatedAtAsync<ProgressEndpoint>(new { Id = "123" });
```

</details>

<details><summary>Error Code & Error Severity support for the 'ThrowError()' method</summary>

A new overload has been added for the `ThrowError()` method where you can supply an error code and an optional severity value as follows:

```cs
ThrowError("Account is locked out!", errorCode: "AccountLocked", severity: Severity.Error, statusCode: 423);
```

</details>


<details><summary>Setting for changing 'default value' binding behavior</summary>

Given a request dto such as the following, which has a nullable value type property:

```cs
public class MyRequest
{
    [QueryParam]
    public int? Age { get; set; }
}
```

and a request is made with an empty parameter value such as:

```yaml
/person?age=
```

the default behavior is to populate the property with the `default value` for that `value type` when model binding, and if the parameter name is also omitted, the property would end up being `null`.

You can now change this behavior so that in case an empty parameter is submitted, the property would end up being `null`, instead of the default value:

```cs
app.UseFastEndpoints(c => c.Binding.UseDefaultValuesForNullableProps = false)
```

Note: the setting applies to all non-STJ binding paths such as route/query/claims/headers/form fields etc.

</details>

## Improvements 🚀

<details><summary>'Configuration Groups' support for refresh token service</summary>

Configuration groups were not previously compatible with the built-in refresh token functionality. You can now group refresh token service endpoints using a group as follows:

```cs
public class AuthGroup : Group
{
    public AuthGroup()
    {
        Configure("users/auth", ep => ep.Options(x => x.Produces(401)));
    }
}

public class UserTokenService : RefreshTokenService<TokenRequest, TokenResponse>
{
    public MyTokenService(IConfiguration config)
    {
        Setup(
            o =>
            {
                o.Endpoint("refresh-token", ep => 
                  { 
                    ep.Summary(s => s.Summary = "this is the refresh token endpoint");
                    ep.Group<AuthGroup>(); // this was not possible before
                  });
            });
    }
}
```

</details>

<details><summary>Pick up Swagger request param example values from summary example</summary>

In the past, the only way to provide an example value for a swagger request parameter was with an xml document comment like so:

```cs
sealed class MyRequest
{
    /// <example>john doe</example>
    public string Name { get; set; }
}
```

The example values will now be picked up from the summary example request properties which you can supply like so:

```cs
Summary(
    s => s.ExampleRequest = new()
    {
        Name = "jane doe"
    });
```

If you provide both, the values from the summary example will take precedence. 

</details>

## Fixes 🪲

<details><summary>Reflection source generator issue with 'required' properties</summary>

If a DTO class had `required` properties with `[JsonIgnore]` attributes such as this:

```cs
sealed class UpdateRequest 
{ 
    [JsonIgnore] 
    public required int Id { get; set; } 
 
    public required string Name { get; set; } 
} 
```

The reflection source generator failed to generate the correct object initialization factory causing a compile error, which has now been corrected.

</details>

<details><summary>'ClearDefaultProduces()' stopped working from global configurator</summary>

If `ClearDefaultProduces()` was called from the global endpoint configurator function, it had no effect due to a regression introduced in `v6.0`, which has now been corrected.

</details>

<details><summary>Incorrect OAS3 spec generation when 'NotNull()' or 'NotEmpty()' validators were used on nested objects</summary>

If a request DTO has complex nested properties and those properties are being validated with either `NotNull()` or `NotEmpty()`, an incorrect swagger3 spec was being generated due to a bug in the "validation schema processor".

</details>

## Breaking Changes ⚠️