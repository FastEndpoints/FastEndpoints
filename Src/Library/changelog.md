---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

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

<details><summary>Standalone package for Job Queues functionality</summary>

The job queuing functionality has also been extracted out to a separate package `FastEndpoints.JobQueues` which can be used independently of the main FE library. No code changes are needed for existing FE projects.

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

<details><summary>'FastEndpoints.HealthChecks' package</summary>

An opt-in library that eliminates boilerplate health check wiring for apps running behind orchestrators such as Kubernetes/.NET Aspire. You can now use the following convenient extension method and it's overloads:

```csharp
// Defaults: 
// - /health/live (liveness)
// - /health/ready (readiness)
builder.Services.AddServiceHealthChecks();

// Custom paths
builder.Services.AddServiceHealthChecks(
    configureOptions: opts =>
    {
        opts.LivePath = "/alive";
        opts.ReadyPath = "/ready";
    });

// Add custom health checks
builder.Services.AddServiceHealthChecks(
    configureChecks: hc =>
    {
        hc.AddNpgSql(connectionString, name: "postgres");
        hc.AddRedis("localhost:6379", name: "redis");
    });
```

Liveness returns 200 if process is alive (no dependency checks). Readiness runs all registered health checks and returns aggregate status with optional JSON response.

</details>

<details><summary>Test/verify command executions in endpoints without mocks/fakes</summary>

In a similar fashion to the previously released [Test Event Receivers](https://gist.github.com/dj-nitehawk/ae85c63fefb1e8163fdd37ca6dcb7bfd) feature, you can now use [Test Command Receivers](https://gist.github.com/dj-nitehawk/abf3fd08bae544ee3bcafb5c5f487c4a) to verify that a certain command execution was initiated by an endpoint or service without having to register fake command handlers.

Typically, you'd unit test the command/handlers in isolation to verify the business logic in those handlers and use the "Test Event/Command Receivers" to validate that certain entrypoints in your application actually do trigger/issue particular commands and events.

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

<details><summary>Routeless test helpers and optional query params issue</summary>

Given a request DTO class such as:

```csharp
public class MyRequest 
{
    [RouteParam]
    public string? Something
}
```

When you don't supply a value for `Something` during testing with the routeless test helpers such as `.GETAsync<>()`, and empty query parameter would be appended to the request URL like so:

```csharp
/my/endpoint?Something=
```

While this isn't inherently wrong, the common behavior in most REST clients, is to not send in a query parameter at all for when the value is empty. The test helpers will also follow suit from now on.

</details>

<details><summary>Request binder exception when request DTO ctor has a optional struct argument</summary>

The default request binder was throwing an exception when a request DTO has an optional constructor argument that is a struct type like the following.

```csharp
sealed class MyRequest(SomeStruct someOptionalStruct = default)
{
  ...
}
```

</details>


[//]: # (## Breaking Changes ⚠️)
