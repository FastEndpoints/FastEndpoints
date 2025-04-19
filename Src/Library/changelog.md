---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Convenience method for retrieving the endpoint name</summary>

You can now obtain the auto generated endpoint name like below for the purpose of custom link generation using the `LinkGenerator` class.

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

A url with query parameters such as this would work out of the box now:

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

## Fixes 🪲

## Breaking Changes ⚠️