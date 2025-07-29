---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

## Improvements 🚀

## Fixes 🪲

<details><summary>Incorrect enum value for JWT security algorithm was used</summary>

The wrong variant (`SecurityAlgorithms.HmacSha256Signature`) was being used for creating symmetric JWTs by default.
The default value has been changed to `SecurityAlgorithms.HmacSha256`. It's recommended to invalidate and regenerate new tokens if you've been using the default.

If for some reason, you'd like to keep using `SecurityAlgorithms.HmacSha256Signature`, you can set it yourself like so:

```csharp
var token = JwtBearer.CreateToken(
    o =>
    {
        o.SigningKey = ...;
        o.SigningAlgorithm = SecurityAlgorithms.HmacSha256Signature;
    });
```

</details>

## Breaking Changes ⚠️