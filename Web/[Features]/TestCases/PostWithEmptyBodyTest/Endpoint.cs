namespace TestCases.PostWithEmptyBodyTest;

public class Response
{
    public int Id { get; set; }
}

public class Endpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Post("test-cases/post-empty-body/{Id}");
        RoutePrefixOverride(string.Empty);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "post with empty body endpoint summary";
        });
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response.Id = Route<int>("Id");

        return SendAsync(Response);
    }
}