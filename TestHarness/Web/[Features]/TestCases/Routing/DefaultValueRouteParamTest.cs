namespace TestCases.Routing;

public class DefaultValueRouteParamTest : Ep
    .Req<DefaultValueRouteParamTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        Put("test-cases/routing/withdefault/{Name=World}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync(req.Name);
    }

    public record Request(string Name);
}
