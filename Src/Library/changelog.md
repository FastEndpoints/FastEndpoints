---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Keyed service injection support</summary>

[Keyed services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services) introduced in .NET 8 can be injected like so:
```csharp
//property injection
[KeyedService("KeyName")]
public IHelloWorldService HelloService { get; set; }

//constructor injection
public MyEndpoint([FromKeyedServices("KeyName")]IHelloWorldService helloScv)
{
    ...
}

//manual resolving
Resolve<IHelloWorldService>("KeyName");
```

</details>

<details><summary>Model binding support for Typed Http Headers</summary>

Typed Http Headers can be bound by simply annotating with a `[FromHeader(...)]` attribute like so:

```csharp
sealed class MyRequest : PlainTextRequest
{
    [FromHeader("Content-Disposition")]
    public ContentDispositionHeaderValue Disposition { get; set; }
}
```

NOTE: Only supported on .Net 8+ and typed header classes from `Microsoft.Net.Http.Headers` namespace.

</details>

<details><summary>Ability to strip symbols from Swagger group/tag names</summary>

Given a route like:

```
/api/admin-dashboard/ticket/{id}
```

And swagger config like this:

```csharp
bld.Services.SwaggerDocument(
    o =>
    {
        o.AutoTagPathSegmentIndex = 2;
        o.TagCase = TagCase.TitleCase;
        o.TagStripSymbols = true; //this option is new
    });
```

The resulting group/tag name will be:

```
AdminDashboard
```

</details>

## Improvements 🚀

<details><summary>Better separation of concerns for integration test-classes</summary>

Previously, the recommendation was to create as many derived `TestFixture<TProgram>` classes as needed and use them as the means to share data/state among multiple test-methods of the same test-class.

A new `StateFixture` abstract class has been introduced. So that your test suit can have just a couple of "App Fixtures"(`AppFixture<TProgram>`) - each representing a uniquely configured SUT(live app/WAF instance), while each test-class can have their own lightweight "StateFixture" for the sole purpose of sharing state/data amongst multiple test-methods of that test-class.

This leads to better test run performance as each unique SUT is only created once no matter how many test classes use the same derived `AppFixture<TProgram>` class. Please re-read the [integration testing doc page](https://fast-endpoints.com/docs/integration-unit-testing#fastendpoints-testing-package) for further clarification.

</details>

<details><summary>Relax DTO type constraint on 'Validator&lt;TRequest&gt;' class</summary>

The type constraint on the `Validator<TRequest>` class has been relaxed to `notnull` so that struct type DTOs can be validated.

</details>

<details><summary>Allow TestFixture's TearDownAsync method to make Http calls</summary>

Previously the `TestFixture<TProgram>` class would dispose the default http client before executing the teardown method. This prevents cleanup code to be able to make http calls. Now the http client is only disposed after `TearDownAsync` has completed.

</details>

<details><summary>Ability to customize job queue storage provider re-check frequency</summary>

You can now customize the job queue storage provider re-check time delay in case you need re-scheduled jobs to execute quicker.

```csharp
app.UseJobQueues( 
    o => 
    { 
        o.StorageProbeDelay = TimeSpan.FromSeconds(5); 
    });
```

</details>

## Fixes 🪲

<details><summary>Swagger UI displaying random text for email fields</summary>

When a FluentValidator rule is attached to a property that's an email address, Swagger UI was displaying a random string of characters instead of showing an email address. This has been rectified.

</details>

<details><summary>Swagger generation issue with form content and empty request DTO</summary>

Endpoints configured like below, where the request dto type is `EmptyRequest` and the endpoint allows form content; was causing the swagger processor to throw an error, which has been rectified.

```csharp
sealed class MyEndpoint : EndpointWithoutRequest<MyResponse>
{
    public override void Configure()
    {
        ...
        AllowFileUploads(); 
    }
}
```

</details>

<details><summary>Swagger issue with reference type DTO props being marked as nullable</summary>

Given a DTO such as this:

```csharp
sealed class MyRequest
{
    public string PropOne { get; set; }
    public string? PropTwo { get; set; }
}
```

The following swagger spec was generated before:

```json
"parameters": [
    {
        "name": "propOne",
        "in": "query",
        "required": true,
        "schema": {
            "type": "string",
            "nullable": true //this is wrong as property is not marked nullable
        }
    },
    {
        "name": "propTwo",
        "in": "query",
        "schema": {
            "type": "string",
            "nullable": true
        }
    }
]
```

Non-nullable reference types are now correctly generated as non-nullable.

</details>

<details><summary>Swagger security processor was unable to handle Minimal Api Endpoints with Auth requirements</summary>

A NRE was being thrown when the swagger security operation processor was encountering minimal api endpoints with auth requirements.

</details>

[//]: # (## Breaking Changes ⚠️)