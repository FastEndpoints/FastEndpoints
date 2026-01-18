---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

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

## Improvements 🚀

<details><summary>Job Queues storage processing ⚠️</summary>

Several optimizations have been done to the job queues storage logic to reduce the number of queries in certain scenarios. Please see the breaking changes section below as one of the methods of `IJobStorageProvider` needs a minor change.

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