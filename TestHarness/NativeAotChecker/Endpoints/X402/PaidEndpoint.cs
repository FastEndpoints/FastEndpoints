namespace NativeAotChecker.Endpoints.X402;

public sealed class PaidResponse
{
    public string Message { get; set; }
}

public sealed class PaidEndpoint : EndpointWithoutRequest<PaidResponse>
{
    public override void Configure()
    {
        Get("x402-paid");
        AllowAnonymous();
        RequirePayment("1000", "aot paid endpoint");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "paid" }, ct);
}
