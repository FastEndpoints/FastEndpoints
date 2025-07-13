using System.Text.Encodings.Web;
using System.Web;

namespace TestCases.HydratedQueryParamGeneratorTest;

public sealed class Request
{
    [FromQuery]
    public NestedClass Nested { get; set; }

    [QueryParam]
    public List<Guid> Guids { get; set; }

    [QueryParam]
    public string? Some { get; set; }

    public record NestedClass(string? First, int Last);
}

sealed class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Get("test-cases/query-param-creation-from-test-helpers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendAsync(HttpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString()));
    }
}