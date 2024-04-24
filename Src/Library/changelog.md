---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>New generic attribute [Group&lt;T&gt;] for attribute based endpoint group configuration</summary>

When using attribute based endpoint configuration, you can now use the generic 'Group<TEndpointGroup>' attribute to specify the group which the endpoint belongs to like so:

```csharp
//group definition class
sealed class Administration : Group
{
    public Administration()
    {
        Configure(
            "admin",
            ep =>
            {
                ep.Description(
                    x => x.Produces(401)
                          .WithTags("administration"));
            });
    }
}

//using generic attribute to associate the endpoint with the above group
[HttpPost("login"), Group<Administration>]
sealed class MyEndpoint : EndpointWithoutRequest
{
    ...
}
```

</details>

<details><summary>Specify a label, summary & description for Swagger request examples</summary>

When specifying multiple swagger request examples, you can now specify the additional info like this:

```csharp
Summary(
    x =>
    {
        x.RequestExamples.Add(
            new(
                new MyRequest { ... },
                "label",
                "summary",
                "description"));
    });
```

</details>

<details><summary>Automatic type inference for route params from route constraints for Swagger</summary>

Given route templates such as the following that has type constraints for route params, it was previously only possible to correctly infer the type of the parameter (for Swagger spec generation) if the parameters are being bound to a request DTO and that DTO has a matching property. The following will now work out of the box and the generated Swagger spec will have the respective parameter type/format.

```csharp
sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("test/{id:int}/{token:guid}/{date:datetime}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var id = Route<int>("id");
        var token = Route<Guid>("token");
        var date = Route<DateTime>("date");

        await SendAsync(new { id, token, date });
    }
}
```

You can register your own route constraint types or even override the default ones like below by updating the Swagger route constraint map:

```csharp
FastEndpoints.Swagger.GlobalConfig.RouteConstraintMap["nonzero"] = typeof(long);
FastEndpoints.Swagger.GlobalConfig.RouteConstraintMap["guid"] = typeof(Guid);
FastEndpoints.Swagger.GlobalConfig.RouteConstraintMap["date"] = typeof(DateTime);
```

</details>

[//]: # (## Improvements 🚀)

## Fixes 🪲

<details><summary>Contention issue resulting in random 415 responses</summary>

There was a possible contention issue that could arise in and extremely niche edge case where the WAFs could be instantiated in quick succession which results in tests failing due to 415 responses being returned randomly. This has been fixed by moving the necessary work to be performed at app startup instead of at the first request for a particular endpoint. More info: #661

</details>

<details><summary>Eliminate potential contention issues with 'AppFixture'</summary>

`AppFixture` abstract class has been improved to use an Async friendly Lazy initialization technique to prevent any chances of more than a single WAF being created per each derived `AppFixture` type in high concurrent situations. Previously we were relying solely on `ConcurrentDictionary`'s thread safety features which did not always give the desired effect. Coupling that with Lazy initialization seems to solve any and all possible contention issues.

</details>

## Breaking Changes ⚠️

<details><summary>The way multiple Swagger request examples are set has been changed</summary>

Previous way:

```csharp
Summary(s =>
{
    s.RequestExamples.Add(new MyRequest {...});
});
```

New way:

```csharp
s.RequestExamples.Add(new(new MyRequest { ... })); // wrapped in a RequestExample class
```

</details>

<details><summary>'PreSetupAsync()' trigger behavior change in `AppFixture` class</summary>

Previously the `PreSetupAsync()` virtual method was run per each test-class instantiation. That behavior does not make much sense as the WAF instance is created and cached just once per test run. The new behavior is more in line with other virtual methods such as `ConfigureApp()` & `ConfigureServices()` that they are only ever run once for the sake of creation of the WAF. This change will only affect you if you've been creating some state such as a `TestContainer` instance in `PreSetupAsync` and later on disposing that container in `TearDownAsync()`. From now on you should not be disposing the container yourself if your derived `AppFixture` class is being used by more than one test-class. See [this gist](https://gist.github.com/dj-nitehawk/04a78cea10f2239eb81c958c52ec84e0) to get a better understanding.

</details>