namespace TestCases.RateLimitTests;

public class GlobalErrorResponseTest : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("test-cases/global-throttle-error-response");
        AllowAnonymous();
        Summary(s =>
            s.Params["OtherID"] = "the description for other id");
        Throttle(3, 120);
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendAsync(new()
        {
            CustomerID = Query<int>("CustomerID"),
            OtherID = Query<int>("OtherID")
        });
    }
}