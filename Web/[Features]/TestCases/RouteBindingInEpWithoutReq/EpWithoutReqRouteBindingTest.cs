namespace TestCases.RouteBindingInEpWithoutReq;

public class EpWithoutReqRouteBindingTest : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("test-cases/ep-witout-req-route-binding-test/{CustomerID}/{OtherID}");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendAsync(new()
        {
            CustomerID = Route<int>("CustomerID"),
            OtherID = Route<int>("OtherID")
        });
    }
}

public class Response
{
    public int CustomerID { get; set; }
    public int? OtherID { get; set; }
}