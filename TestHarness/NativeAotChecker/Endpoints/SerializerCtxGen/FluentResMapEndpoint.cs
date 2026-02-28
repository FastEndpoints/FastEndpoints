namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentMapRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

sealed class FluentMapResponse
{
    public string FullName { get; set; }
    public int Age { get; set; }
    public bool MapperWasUsed { get; set; }
}

sealed class FluentMapEntity
{
    public string FullName { get; set; }
    public int Age { get; set; }
}

sealed class FluentMapMapper : Mapper<FluentMapRequest, FluentMapResponse, FluentMapEntity>
{
    public override FluentMapEntity ToEntity(FluentMapRequest r)
        => new()
        {
            FullName = $"{r.FirstName} {r.LastName}",
            Age = r.Age
        };

    public override FluentMapResponse FromEntity(FluentMapEntity e)
        => new()
        {
            FullName = e.FullName,
            Age = e.Age,
            MapperWasUsed = true
        };
}

sealed class FluentResMapEndpoint : Ep.Req<FluentMapRequest>.Res<FluentMapResponse>.Map<FluentMapMapper>
{
    public override void Configure()
    {
        Post("fluent-req-res-map");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentMapRequest r, CancellationToken c)
    {
        var entity = Map.ToEntity(r);
        var response = Map.FromEntity(entity);
        await Send.OkAsync(response, c);
    }
}
