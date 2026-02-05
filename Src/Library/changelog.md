---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Auto generate STJ JsonSerializationContexts</summary>

You no longer need to ever see a `JsonSerializerContext` thanks to the new serializer context generator in FastEndpoints. (Unless you want to that is 😜). See the documentation [here](https://fast-endpoints.com/docs/model-binding#auto-generate-stj-serializer-contexts) on how to enable it.

</details>

<details><summary>Response sending method 'NotModifiedAsync'</summary>

A new response sending method has been added for sending a 304 status code response.

```csharp
public override async Task HandleAsync(CancellationToken c)
{
    await Send.NotModifiedAsync();
}
```

</details>

## Fixes 🪲

<details><summary>Index out of range exception in routeless test helpers</summary>

The routeless integration test helpers such as `.GETAsync<>()` would throw an exception when testing an endpoint configured with the root URL `/`, which has now been fixed.

</details>

<details><summary>Query/Route param culture mismatch with routeless test helpers and backend</summary>

The routeless test helpers such as `.GETAsync<>()` would construct route/query params (of certain primitives such as `DateTime`) using the culture of the machine where the tests are being run, while the application is set up to use a different culture, the tests would fail. This has been solved by constructing route/query params for primitive/`IFormattable` types using ISO compliant invariant culture format when constructing the requests.

</details>

<details><summary>Routeless testing helpers contention issue</summary>

The test helpers were using a regular dictionary to cache test URLs internally which could sometimes cause trouble under high load. This has been solved by switching to a concurrent dictionary.

</details>

## Improvements 🚀

<details><summary>Job Queues storage processing ⚠️</summary>

Several optimizations have been done to the job queues storage logic to reduce the number of queries in certain scenarios. Please see the breaking changes section below as one of the methods of `IJobStorageProvider` needs a minor change.

</details>

<details><summary>Easy access to error response content with testing helpers</summary>

You can now easily inspect why a request failed when you expected it to succeed. There's now a new string property `ErrorContent` on the `TestResult` record that routeless testing helpers return.

```csharp
[Fact]
public async Task Get_Request_Responds_With_200_Ok()
{
    var (rsp, res, errorContent) = await app.Client.GETAsync<MyEndpoint, MyRequest, string>(new() { ... });

    if (rsp.IsSuccessStatusCode)
        Assert.True(...);
    else
        Assert.Fail(errorContent); //errorContent contains the error response body as a string
}
```

</details>

<details><summary>Request DTO serialization behavior of testing helpers</summary>

Testing helpers such as `.POSTAsync<>()` will only serialize the request DTO in to the request body if there's at least one property on the DTO will be bound from the JSON body. In instances where nothing should be bound from the JSON body, the request body content will be empty.

</details>

<details><summary>Mitigate incorrect service scoping due to user error in Command Bus</summary>

If a user for whatever reason registerd command handlers as scoped services in DI themselves (when they're not supposed to), it could lead to unexpected behavior. This is no longer an issue.

</details>

## Minor Breaking Changes ⚠️

<details><summary>'IJobStorageProvider.GetNextBatchAsync()' return type change</summary>

As a result of optimizations done to the storage processing logic in job queues, your job storage provider implementation requires minor change from:

```csharp
public Task<IEnumerable<...>> GetNextBatchAsync(...)
```

to:

```csharp
public Task<ICollection<...>> GetNextBatchAsync(...)
```

You are now required to return a materialized collection instead of an `IEnumerable<T>`.

</details>