namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class FluentNoReqResResponse
{
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}

sealed class FluentNoReqResEndpoint : Ep.NoReq.Res<FluentNoReqResResponse>
{
    public override void Configure()
    {
        Get("fluent-noreq-res");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Message = "Hello from NoReq.Res",
                Timestamp = DateTime.UtcNow
            },
            ct);
    }
}