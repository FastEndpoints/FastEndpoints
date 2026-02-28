namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentNoReqNoResEndpoint : Ep.NoReq.NoRes
{
    public override void Configure()
    {
        Get("fluent-noreq-nores");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("Hello from NoReq.NoRes");
    }
}