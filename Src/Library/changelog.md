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