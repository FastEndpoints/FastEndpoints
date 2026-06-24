using Microsoft.AspNetCore.Authorization;

namespace TestCases.QueryMethodTest;

public class QueryRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }

    [QueryParam]
    public string? Filter { get; set; }

    [FromHeader("x-query-token")]
    public string? Token { get; set; }
}

public class QueryResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Filter { get; set; }
    public string? Token { get; set; }
    public string? Method { get; set; }
}

public class FluentEndpoint : Endpoint<QueryRequest, QueryResponse>
{
    public override void Configure()
    {
        Query("/test-cases/query-method/fluent/{id}");
        AllowAnonymous();
    }

    public override Task<QueryResponse> ExecuteAsync(QueryRequest req, CancellationToken ct)
        => Task.FromResult(CreateResponse(req, HttpMethod));

    internal static QueryResponse CreateResponse(QueryRequest req, Http method)
        => new()
        {
            Id = req.Id,
            Name = req.Name,
            Filter = req.Filter,
            Token = req.Token,
            Method = method.ToString()
        };
}

[HttpQuery("/test-cases/query-method/attribute/{id}"), AllowAnonymous]
public class AttributeEndpoint : Endpoint<QueryRequest, QueryResponse>
{
    public override Task<QueryResponse> ExecuteAsync(QueryRequest req, CancellationToken ct)
        => Task.FromResult(FluentEndpoint.CreateResponse(req, HttpMethod));
}

public class AntiforgeryEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Query("/test-cases/query-method/antiforgery");
        AllowAnonymous();
        EnableAntiforgery();
        AllowFileUploads();
    }

    public override Task<string> ExecuteAsync(CancellationToken ct)
        => Task.FromResult("query antiforgery skipped");
}