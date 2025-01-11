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

<details><summary>Value parser support for Reflection Source Generator</summary>

Value parser functions (used by non-stj model binding) will now be source generated instead of being compiled at runtime when you opt-in to use the reflection source generator.

</details>

<details><summary>Optimize value parser internals</summary>

String value parsing logic used in most non-stj model binding paths has been simplified and optimized to reduce allocations and unnecessary boxing.

</details>

<details><summary>Graceful shutdown of Job Queue processing</summary>

If app shutdown is requested during a retry loop (due to transient failures) in job queue processing, the operation will now be tried at least once before exiting the retry loops and allowing the app to shut down.

</details>

## Fixes 🪲

<details><summary>Issue with unit testing endpoints with pre-processors with injected dependencies</summary>

Unit tests were failing to instantiate pre-processors that had injected dependencies due to a small oversight in the `ServiceResolver` code with regards to how singletons were instantiated, which has been fixed.

</details>

<details><summary>Potential infinite recursion in Swagger Processor due to circular references</summary>

In certain edge cases where the schema has circular references, there was a potential inifinite recursion issue which could lead to memory leaks when generating the swagger docs.

</details>

## Breaking Changes ⚠️