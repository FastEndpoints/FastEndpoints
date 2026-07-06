namespace TestCases.Routing;

public class CatchAllRouteParamTest : Ep
    .Req<CatchAllRouteParamTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        Put("test-cases/routing/catchall/{**Rest}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync(req.Rest);
    }

    public record Request(string Rest);
}
