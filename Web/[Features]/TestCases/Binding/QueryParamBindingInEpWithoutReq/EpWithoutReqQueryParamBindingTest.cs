namespace TestCases.QueryParamBindingInEpWithoutReq;

public class EpWithoutReqQueryParamBindingTest : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("test-cases/ep-witout-req-query-param-binding-test");
        AllowAnonymous();
        Summary(
            s =>
                s.Params["otherID"] = "the description for other id");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(
            new()
            {
                CustomerID = Query<int>("customerId"),
                OtherID = Query<int>("otherId"),
                Doubles = Query<double[]>("doubles")!,
                Guids = Query<List<Guid>>("guids")!,
                Ints = Query<IEnumerable<int>>("ints")!,
                Floaty = Query<float>("floaty")
            });
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