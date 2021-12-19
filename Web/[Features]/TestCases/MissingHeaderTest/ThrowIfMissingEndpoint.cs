namespace TestCases.MissingHeaderTest;

public class ThrowIfMissingEndpoint : Endpoint<ThrowIfMissingRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/missing-header-test");
    }

    public override Task HandleAsync(ThrowIfMissingRequest req, CancellationToken ct)
    {
        //this line will never be reached as ErrorResponse will be sent due to claim missing
        return Task.CompletedTask;
    }
}
