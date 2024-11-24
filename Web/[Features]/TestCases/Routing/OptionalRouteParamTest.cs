namespace TestCases.Routing;

public class OptionalRouteParamTest : Ep
    .Req<OptionalRouteParamTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        Post("test-cases/routing/offer/{OfferId?}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await SendAsync(req.OfferId ?? "default offer");
    }

    public record Request(string? OfferId);
}