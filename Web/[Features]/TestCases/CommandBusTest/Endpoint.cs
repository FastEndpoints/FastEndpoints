namespace TestCases.CommandBusTest;

public class Endpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/tests/commands");
        AllowAnonymous();
    }

    public async override Task<string> ExecuteAsync(CancellationToken ct)
    {
        await new TestVoidCommand().ExecuteAsync(ct);
        var result = await new TestCommand() { FirstName = "x", LastName = "y" }.ExecuteAsync(ct);
        return result;
    }
}