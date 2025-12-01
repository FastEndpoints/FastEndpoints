---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

## Improvements 🚀

<details><summary>Strong-Name-Signed Assemblies</summary>

All FastEndpoints assemblies are now strong-name-signed. This only matters if your project is utilizing assembly signing. Signed projects will no longer show warnings about FastEndpoints not being signed.

</details>

## Fixes 🪲

<details><summary>Group summary overriding endpoint level summary data</summary>

There was an oversight that resulted in endpoint level summary data being overwritten by group level summary data in situations such as the following:

```csharp
sealed class MyGroup : Group
{
    public MyGroup()
    {
        Configure("group", ep => ep.Summary(s => s.Description = "group level text"));
    }
}

sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/something");
        Group<MyGroup>();
        Summary(s => s.Description = "endpoint level text"); //this would get loss due to the bug
    }
}
```

</details>

## Breaking Changes ⚠️