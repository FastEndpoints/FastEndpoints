namespace TestCases.HydratedQueryParamGeneratorTest;

public sealed class Request
{
    [FromQuery] //this is the right way to bind complex data from query params
    public NestedClass Nested { get; set; }

    [QueryParam]
    public List<Guid> Guids { get; set; }

    [QueryParam]
    public string? Some { get; set; }

    [RouteParam]
    public ComplexIdClass ComplexId { get; set; }

    [RouteParam]
    public ComplexIdClassWithToString ComplexIdString { get; set; }

    public record NestedClass(string? First, int Last);

    public class ComplexIdClass
    {
        public int Number1 { get; set; }
        public int Number2 { get; set; }
    }

    public class ComplexIdClassWithToString
    {
        public int Number1 { get; set; }
        public int Number2 { get; set; }

        public override string ToString()
            => $"{Number1}:{Number2}";

        public static bool TryParse(string? s, out ComplexIdClassWithToString? result)
        {
            var parts = s?.Split(':');

            if (parts?.Length < 2)
            {
                result = null;

                return false;
            }

            result = new()
            {
                Number1 = int.Parse(parts![0]),
                Number2 = int.Parse(parts[1])
            };

            return true;
        }
    }
}

public sealed class Response
{
    public string Nested { get; set; }
    public string Guids { get; set; }
    public string Some { get; set; }
    public string ComplexId { get; set; }
    public string ComplexIdString { get; set; }
}

sealed class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("test-cases/query-param-creation-from-test-helpers/{ComplexId}/{ComplexIdString}");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        var pathSegments = HttpContext.Request.Path.Value?.Split('/');

        //we only care about the correct querystring in this test
        Response = new()
        {
            Nested = HttpContext.Request.Query["nested"]!,
            Guids = HttpContext.Request.Query["guids"]!,
            Some = HttpContext.Request.Query["some"]!,
            ComplexId = pathSegments?[^2] ?? "",
            ComplexIdString = pathSegments?[^1] ?? ""
        };

        return Task.CompletedTask;
    }
}