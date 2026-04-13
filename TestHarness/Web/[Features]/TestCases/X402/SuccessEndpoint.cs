namespace TestCases.X402;

sealed class SuccessEndpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/test-cases/x402/success");
        AllowAnonymous();
        RequirePayment("1000", "Protected success endpoint");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "success" }, ct);
}