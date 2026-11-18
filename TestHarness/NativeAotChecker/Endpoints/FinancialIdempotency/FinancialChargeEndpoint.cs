namespace NativeAotChecker.Endpoints.FinancialIdempotency;

public sealed class FinancialChargeRequest
{
    public int Amount { get; set; }
}

public sealed class FinancialChargeResponse
{
    public int Amount { get; set; }
    public int Execution { get; set; }
}

public sealed class FinancialChargeEndpoint : Endpoint<FinancialChargeRequest, FinancialChargeResponse>
{
    static int _executions;

    public override void Configure()
    {
        Post("financial-charge");
        AllowAnonymous();
        FinancialIdempotency();
    }

    public override Task HandleAsync(FinancialChargeRequest r, CancellationToken ct)
        => Send.OkAsync(new() { Amount = r.Amount, Execution = Interlocked.Increment(ref _executions) }, ct);
}
