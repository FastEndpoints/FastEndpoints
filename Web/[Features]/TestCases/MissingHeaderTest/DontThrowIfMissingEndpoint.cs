namespace TestCases.MissingHeaderTest;

public class DontThrowIfMissingEndpoint : Endpoint<DontThrowIfMissingRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/missing-header-test/dont-throw");
    }

    public override Task HandleAsync(DontThrowIfMissingRequest req, CancellationToken ct)
    {
        return SendAsync($"you sent {req.TenantID}");
    }
}
