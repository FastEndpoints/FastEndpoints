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
            OtherID = Query<int>("OtherID"),
            Doubles = Query<double[]>("Doubles"),
            Guids = Query<List<Guid>>("Guids"),
            Ints = Query<IEnumerable<int>>("Ints"),
            Floaty = Query<float>("Floaty")
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

    public double[] Doubles { get; set; }

    public IEnumerable<int> Ints { get; set; }

    public List<Guid> Guids { get; set; }

    public float Floaty { get; set; }
}