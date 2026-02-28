namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentNoResRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

sealed class FluentNoResEndpoint : Ep.Req<FluentNoResRequest>.NoRes
{
    public override void Configure()
    {
        Post("fluent-req-nores");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentNoResRequest req, CancellationToken ct)
    {
        await Send.OkAsync($"{req.FirstName} {req.LastName}");
    }
}