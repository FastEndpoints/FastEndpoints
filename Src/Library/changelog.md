---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Support for Native AOT compilation</summary>

FastEndpoints is now Native AOT compatible. Please see the [documentation here](https://fast-endpoints.com/docs/native-aot) on how to configure it.

If you'd like to jump in head first, a fresh AOT primed starter project can be scaffolded like so:

```sh
dotnet new install FastEndpoints.TemplatePack
dotnet new feaot -n MyProject
```

If you've not worked with AOT compilation in .NET before, it's highly recommended to read the docs linked above.

</details>

<details><summary>Auto generate STJ JsonSerializationContexts</summary>

You no longer need to ever see a `JsonSerializerContext` thanks to the new serializer context generator in FastEndpoints. (Unless you want to that is 😉). See the documentation [here](https://fast-endpoints.com/docs/model-binding#auto-generate-stj-serializer-contexts) on how to enable it for non-AOT projects.

</details>

<details><summary>Distributed job processing support</summary>

The job queueing functionality now has support for distributed workers that connect to the same underlying database. See the documentation [here](https://fast-endpoints.com/docs/job-queues#distributed-job-processing).

</details>

<details><summary>Qualify endpoints in global configurator according to endpoint level metadata</summary>

You can now register any object as metadata at the endpoint level like so:

```csharp
sealed class SomeObject
{
    public int Id { get; set; }
    public bool Yes { get; set; }
}

sealed class MetaDataRegistrationEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/test-cases/endpoint-metadata-reg-test");
        Metadata(
            new SomeObject { Id = 1, Yes = true },
            new SomeObject { Id = 2, Yes = false });
    }
}
```

and configure endpoints conditionally at startup according to the endpoint level metadata that was added by the endpoint configure method:

```csharp
app.UseFastEndpoints(
       c => c.Endpoints.Configurator =
                ep =>
                {
                    if (ep.EndpointMetadata?.OfType<SomeObject>().Any(s => s.Yes) is true)
                        ep.AllowAnonymous();
                })
```

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

<details><summary>Source generators having trouble with special characters in project names</summary>

The source generators were generating incorrect namespaces if the project name has dashes such as `My-Project.csproj` which would result in generating namespaces with dashes, which is invalid for C#.

</details>

<details><summary>Test collection ordering regression</summary>

Due to an internal behavior change in XUnit v3, test collection ordering was being overriden by XUnit. This has been rectified by taking matters in to our own hands and bypassing XUnit's collection runner.

</details>

## Improvements 🚀

<details><summary>Job Queues storage processing</summary>

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

Testing helpers such as `.POSTAsync<>()` will only serialize the request DTO in to the request body if there's at least one property on the DTO that will be bound from the JSON body. In instances where nothing should be bound from the JSON body, the request body content will be empty.

</details>

<details><summary>Mitigate incorrect service scoping due to user error in Command Bus</summary>

If a user for whatever reason registerd command handlers as scoped services in DI themselves (when they're not supposed to), it could lead to unexpected behavior. This is no longer an issue.

</details>

## Minor Breaking Changes ⚠️

<details><summary>New 'IJobStorageProvider.DistributedJobProcessingEnabled' property</summary>

Due to adding support for distributed job processing, all storage provider implementations must now implement the following boolean property. Simply set it to `false` when not using distributed job processing like so:

```cs
sealed class JobStorageProvider : IJobStorageProvider<JobRecord>
{
    public bool DistributedJobProcessingEnabled => false;
}
```

</details>

<details><summary>New 'IJobStorageRecord.DequeueAfter' property</summary>

Even though you only need to implement this property when using distributed job processing, it's recommended to either let all the jobs in your database run to completion before upgrading to `v8.0` and/or run a migration to set the value of `DequeueAfter` to `DateTime.MinValue` for all jobs already in the database to ensure that they get picked up properly for processing. (Even when not using distributed processing.)

</details>

<details><summary>'IJobStorageProvider.GetNextBatchAsync()' return type change</summary>

As a result of optimizations done to the storage processing logic in job queues, your job storage provider implementation requires a minor change from:

```csharp
public Task<IEnumerable<...>> GetNextBatchAsync(...)
```

to:

```csharp
public Task<ICollection<...>> GetNextBatchAsync(...)
```

You are now required to return a materialized collection instead of an `IEnumerable<T>`.

</details>