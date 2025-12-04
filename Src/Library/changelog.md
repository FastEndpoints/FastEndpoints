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

<details><summary>Aspire Testing support for routeless test helpers</summary>

You can now use the routeless test helpers such as `.GETAsync<MyEndpoint>()` with Aspire `DistributedApplication` testing like so:

```csharp
[Fact]
public async Task Endpoint_Returns_Ok_Response()
{
    // Arrange
    var ct = TestContext.Current.CancellationToken;
    var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireApp_AppHost>(ct);
    await using var app = await appHost.BuildAsync(ct).WaitAsync(_defaultTimeout, ct);
    await app.StartAsync(ct).WaitAsync(_defaultTimeout, ct);
    await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", ct).WaitAsync(_defaultTimeout, ct);

    // Act
    var httpClient = app.CreateHttpClient("apiservice");
    var (response, _) = await httpClient.GETAsync<HelloEndpoint, EmptyResponse>();

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

</details>

## Improvements 🚀

<details><summary>Strong-Name-Signed Assemblies</summary>

All FastEndpoints assemblies are now strong-name-signed. This only matters if your project is utilizing assembly signing. Signed projects will no longer show warnings about FastEndpoints not being signed.

</details>

<details><summary>Increased frequency of stream flushing in SSE</summary>

SSE streams are now flushed after each write as opposed to after every batch. This eliminates random pauses on the client-side due to stream buffering.

</details>

<details><summary>Job storage performance optimization</summary>

Optimized the `JobQueue` startup initialization by reusing the results of the existing scheduled-jobs query to determine whether the queue is in use, allowing the system to skip the additional query for future scheduled jobs when current jobs are already present, thereby reducing database load and improving startup performance without changing observable behavior.

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