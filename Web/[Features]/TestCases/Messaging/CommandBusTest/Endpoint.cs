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
        await new VoidCommand() { FirstName = "x", LastName = "y" }.ExecuteAsync(ct);
        return await new SomeCommand() { FirstName = "x", LastName = "y" }.ExecuteAsync(ct);
    }
}