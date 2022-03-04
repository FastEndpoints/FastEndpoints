namespace TestCases.RouteBindingInEpWithoutReq;

public class EpWithoutReqRouteBindingTest : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("test-cases/ep-witout-req-route-binding-test/{CustomerID:int}/{OtherID}");
        AllowAnonymous();
        Summary(s =>
            s.Params["OtherID"] = "the description for other id");
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

    /// <summary>
    /// optional other id
    /// </summary>
    public int? OtherID { get; set; }
}