---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Standalone package for Event/Command Bus functionality</summary>

The in-process Event Bus and Command Bus features have been liberated from the clutches of the FastEndpoints main library. A new, independent `FastEndpoints.Messaging` package has been created. This package can be used in any .NET 8+ application, even with Blazor WASM. Simply install the nuget package and register it with the IOC container like so:

```csharp
builder.Services.AddMessaging();
var host = builder.Build();
host.Services.UseMessaging();
```

There's no setup (nor code changes) needed for projects using FastEndpoints main library. The above is only for when you want to use the messaging functionality in projects that don't have FastEndpoints.

</details>

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