namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentResRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

sealed class FluentResResponse
{
    public string FullName { get; set; }
}

sealed class FluentResEndpoint : Ep.Req<FluentResRequest>.Res<FluentResResponse>
{
    public override void Configure()
    {
        Post("fluent-req-res");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentResRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new FluentResResponse { FullName = $"{req.FirstName} {req.LastName}" }, ct);
    }
}
