using FastEndpoints;

namespace FEBench;

public class QueryRequest
{
    [FromQueryParams]
    public QueryObject? Query { get; set; }
}
public class QueryObject
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
    public NestedQueryObject? NestedQueryObject { get; set; }
}
public class NestedQueryObject
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
    public MoreNestedQueryObject? MoreNestedQueryObject { get; set; }
}

public class MoreNestedQueryObject
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class QueryResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
    public NestedQueryObject? NestedQueryObject { get; set; }
}

public class QueryRequestEndpoint : Endpoint<QueryRequest, QueryResponse>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/benchmark/query-binding");
        AllowAnonymous();
    }

    public override Task HandleAsync(QueryRequest req, CancellationToken ct)
    {
        return SendAsync(new QueryResponse()
        {
            Id = req.Query!.Id,
            Name = req.Query.FirstName + " " + req.Query.LastName,
            Age = req.Query.Age,
            PhoneNumber = req.Query.PhoneNumbers?.FirstOrDefault(),
            NestedQueryObject = req.Query.NestedQueryObject
        });
    }
}