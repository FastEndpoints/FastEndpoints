namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentNoReqResMapResponse
{
    public string FullName { get; set; }
    public int Age { get; set; }
    public bool MapperWasUsed { get; set; }
}

sealed class FluentNoReqResMapEntity
{
    public string FullName { get; set; }
    public int Age { get; set; }
}

sealed class FluentNoReqResMapMapper : ResponseMapper<FluentNoReqResMapResponse, FluentNoReqResMapEntity>
{
    public override FluentNoReqResMapResponse FromEntity(FluentNoReqResMapEntity e)
        => new()
        {
            FullName = e.FullName,
            Age = e.Age,
            MapperWasUsed = true
        };
}

sealed class FluentNoReqResMapEndpoint : Ep.NoReq.Res<FluentNoReqResMapResponse>.Map<FluentNoReqResMapMapper>
{
    public override void Configure()
    {
        Get("fluent-noreq-res-map");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var entity = new FluentNoReqResMapEntity
        {
            FullName = "John Doe",
            Age = 30
        };
        var response = Map.FromEntity(entity);
        await Send.OkAsync(response, c);
    }
}
