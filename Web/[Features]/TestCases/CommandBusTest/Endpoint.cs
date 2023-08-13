namespace TestCases.CommandBusTest;

public class Endpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/tests/commands");
        AllowAnonymous();
    }

    public override async Task<string> ExecuteAsync(CancellationToken ct)
    {
        await new TestVoidCommand() { FirstName = "x", LastName = "y" }.ExecuteAsync(ct);
        return await new TestCommand() { FirstName = "x", LastName = "y" }.ExecuteAsync(ct);
    }
}