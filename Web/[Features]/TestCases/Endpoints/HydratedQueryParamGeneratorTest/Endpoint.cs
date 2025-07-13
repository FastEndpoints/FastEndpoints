namespace TestCases.HydratedQueryParamGeneratorTest;

public sealed class Request
{
    [FromQuery] //this is the right way to bind complex data from query params
    public NestedClass Nested { get; set; }

    [QueryParam]
    public List<Guid> Guids { get; set; }

    [QueryParam]
    public string? Some { get; set; }

    public record NestedClass(string? First, int Last);
}

public sealed class Response
{
    public string Nested { get; set; }
    public string Guids { get; set; }
    public string Some { get; set; }
}

sealed class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("test-cases/query-param-creation-from-test-helpers");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        //we only care about the correct querystring in this test
        Response = new()
        {
            Nested = HttpContext.Request.Query["nested"]!,
            Guids = HttpContext.Request.Query["guids"]!,
            Some = HttpContext.Request.Query["some"]!
        };

        return Task.CompletedTask;
    }
}