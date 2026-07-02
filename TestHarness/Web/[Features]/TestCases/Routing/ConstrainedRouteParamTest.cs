namespace TestCases.Routing;

public class ConstrainedRouteParamTest : Ep
    .Req<ConstrainedRouteParamTest.Request>
    .Res<int>
{
    public override void Configure()
    {
        Put("test-cases/routing/constrained/{Id:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync(req.Id);
    }

    public record Request(int Id);
}
