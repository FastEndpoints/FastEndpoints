namespace TestCases.MissingClaimTest;

public class DontThrowIfMissingEndpoint : Endpoint<DontThrowIfMissingRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/missing-claim-test/dont-throw");
    }

    public override Task HandleAsync(DontThrowIfMissingRequest req, CancellationToken ct)
    {
        return SendAsync($"you sent {req.TestProp}");
    }
}
