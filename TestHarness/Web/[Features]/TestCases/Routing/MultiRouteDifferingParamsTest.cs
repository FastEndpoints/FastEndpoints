namespace TestCases.Routing;

public class MultiRouteDifferingParamsTest : Ep
    .Req<MultiRouteDifferingParamsTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        Put("test-cases/routing/multiroute/full/{Id}/{Name}", "test-cases/routing/multiroute/partial/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync($"{req.Id}:{req.Name}");
    }

    public record Request(int Id, string? Name);
}
