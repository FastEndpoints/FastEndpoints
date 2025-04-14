---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

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