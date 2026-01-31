using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Empty request with query params only in AOT mode
public sealed class QueryOnlyRequest
{
    [QueryParam]
    public string Search { get; set; } = string.Empty;

    [QueryParam]
    public int Page { get; set; } = 1;

    [QueryParam]
    public int PageSize { get; set; } = 10;

    [QueryParam]
    public string? SortBy { get; set; }

    [QueryParam]
    public bool Ascending { get; set; } = true;
}

public sealed class QueryOnlyResponse
{
    public string Search { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool Ascending { get; set; }
    public int Skip { get; set; }
    public bool QueryParamsBound { get; set; }
}

public sealed class QueryOnlyEndpoint : Endpoint<QueryOnlyRequest, QueryOnlyResponse>
{
    public override void Configure()
    {
        Get("query-only-test");
        AllowAnonymous();
        SerializerContext<QueryOnlySerCtx>();
    }

    public override async Task HandleAsync(QueryOnlyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new QueryOnlyResponse
        {
            Search = req.Search,
            Page = req.Page,
            PageSize = req.PageSize,
            SortBy = req.SortBy,
            Ascending = req.Ascending,
            Skip = (req.Page - 1) * req.PageSize,
            QueryParamsBound = !string.IsNullOrEmpty(req.Search) || req.Page > 1 || req.PageSize != 10
        }, ct);
    }
}

[JsonSerializable(typeof(QueryOnlyRequest))]
[JsonSerializable(typeof(QueryOnlyResponse))]
public partial class QueryOnlySerCtx : JsonSerializerContext;
