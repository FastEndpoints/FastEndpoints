namespace TestCases.X402;

sealed class PreSettledEndpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/test-cases/x402/pre-settled");
        AllowAnonymous();
        RequirePayment(
            "1000",
            "Protected pre-settled endpoint",
            o => o.SettlementMode = Settle.BeforeHandler);
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "pre-settled" }, ct);
}