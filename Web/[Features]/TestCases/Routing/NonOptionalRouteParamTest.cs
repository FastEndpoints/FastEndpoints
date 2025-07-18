namespace TestCases.Routing;

public class NonOptionalRouteParamTest : Ep
    .Req<NonOptionalRouteParamTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        Post("test-cases/routing/user/{UserId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.ResponseAsync(req.UserId);
    }

    public record Request(string UserId);
}