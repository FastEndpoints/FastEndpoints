---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Specify a request binder per group</summary>

It is now possible to register a particular open generic request binder such as the following:

```csharp
class MyBinder<TRequest> : RequestBinder<TRequest> where TRequest : notnull 
{ 
    public override async ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct) 
    { 
        var req = await base.BindAsync(ctx, ct); // run the default binding logic
 
        if (req is MyRequest r) 
            r.SomeValue = Guid.NewGuid().ToString(); // do whatever you like
 
        return req; 
    } 
} 
```

only for a certain group configuration, so that only endpoints of that group will have the above custom binder associated with them.

```csharp
sealed class MyGroup : Group 
{ 
    public MyGroup() 
    { 
        Configure("/my-group", ep => ep.RequestBinder(typeof(MyBinder<>))); 
    } 
} 
```

</details>

## Improvements 🚀

<details><summary>SSE response standard compliance</summary>

The SSE response implementation has been enhanced by making the `Id` property in `StreamItem` optional, adding an optional `Retry` property for client-side reconnection control, as well as introducing an extra `StreamItem` constructor overload for more flexibility. Additionally, the `X-Accel-Buffering: no` response header is now automatically sent to improve compatibility with reverse proxies like NGINX, ensuring streamed data is delivered without buffering. You can now do the following when doing multi-type data responses:

```csharp
yield return new StreamItem("my-event", myData, 3000);
```

</details>

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