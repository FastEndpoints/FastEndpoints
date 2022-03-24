namespace TestCases.QueryParamBindingInEpWithoutReq;

public class EpWithoutReqQueryParamBindingTest : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("test-cases/ep-witout-req-query-param-binding-test");
        AllowAnonymous();
        Summary(s =>
            s.Params["OtherID"] = "the description for other id");
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

public class Response
{
    [BindFrom("customerId")]
    public int CustomerID { get; set; }

    /// <summary>
    /// optional other id
    /// </summary>
    [BindFrom("otherID")]
    public int? OtherID { get; set; }
}