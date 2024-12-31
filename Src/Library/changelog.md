---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

## Improvements 🚀

<details><summary>Global 'JwtCreationOptions' support for refresh token service</summary>

If you configure jwt creation options at a global level like so:

```cs
bld.Services.Configure<JwtCreationOptions>( o =>  o.SigningKey = "..." ); 
```

The `RefreshTokenService` will now take the default values from the global config if you don't specify anything when configuring the token service like below:

```cs
sealed class MyTokenService : RefreshTokenService<TokenRequest, TokenResponse>
{
    public MyTokenService
    {
        Setup(o =>
        {         
            //no need to specify token signing key/style/etc. here unless you want to.
            o.Endpoint("/api/refresh-token");
            o.AccessTokenValidity = TimeSpan.FromMinutes(5);
            o.RefreshTokenValidity = TimeSpan.FromHours(4);
        });
    }
}
```

</details>

## Fixes 🪲

## Breaking Changes ⚠️