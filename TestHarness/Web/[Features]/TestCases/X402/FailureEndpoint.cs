namespace TestCases.X402;

sealed class FailureEndpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/test-cases/x402/failure");
        AllowAnonymous();
        RequirePayment("1000", "Protected failure endpoint");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.ResponseAsync(new() { Message = "failure" }, 500, ct);
}