namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentNoResMapRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

sealed class FluentNoResMapEntity
{
    public string FullName { get; set; }
    public int Age { get; set; }
}

sealed class FluentNoResMapMapper : RequestMapper<FluentNoResMapRequest, FluentNoResMapEntity>
{
    public override FluentNoResMapEntity ToEntity(FluentNoResMapRequest r)
        => new()
        {
            FullName = $"{r.FirstName} {r.LastName}",
            Age = r.Age
        };
}

sealed class FluentNoResMapEndpoint : Ep.Req<FluentNoResMapRequest>.NoRes.Map<FluentNoResMapMapper>
{
    public override void Configure()
    {
        Post("fluent-req-nores-map");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentNoResMapRequest r, CancellationToken c)
    {
        var entity = Map.ToEntity(r);
        await Send.OkAsync($"Created: {entity.FullName} (age {entity.Age})");
    }
}