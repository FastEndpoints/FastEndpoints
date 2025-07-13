namespace TestCases.HydratedQueryParamGeneratorTest;

public sealed class Request
{
    [QueryParam]
    public NestedClass Nested { get; set; }

    [QueryParam]
    public List<Guid> Guids { get; set; }

    [QueryParam]
    public string? Some { get; set; }

    public record NestedClass(string? First, int Last);
}

public sealed class Response
{
    public Request.NestedClass Nested { get; set; }

    public List<Guid> Guids { get; set; }

    public string? Some { get; set; }
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
        Response = new Response { Nested = r.Nested, Guids = r.Guids, Some = r.Some };
        return Task.CompletedTask;
    }
}