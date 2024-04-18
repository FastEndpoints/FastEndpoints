---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

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

[//]: # (## Fixes 🪲)

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