using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Nested type for [FromQuery] binding
public class SearchFilter
{
    public string Category { get; set; } = string.Empty;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public List<string> Tags { get; set; } = [];
}

// Request with [FromQuery] complex binding
public class ComplexQueryRequest
{
    /// <summary>
    /// Complex nested object bound from query parameters
    /// </summary>
    [FromQuery]
    public SearchFilter? Filter { get; set; }

    /// <summary>
    /// Simple query parameter
    /// </summary>
    public string SortBy { get; set; } = string.Empty;

    /// <summary>
    /// Simple query parameter
    /// </summary>
    public bool Ascending { get; set; }

    /// <summary>
    /// Simple query parameter
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Simple query parameter
    /// </summary>
    public int PageSize { get; set; } = 10;
}

public class ComplexQueryResponse
{
    public string Category { get; set; } = string.Empty;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public int TagCount { get; set; }
    public string SortBy { get; set; } = string.Empty;
    public bool Ascending { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool ComplexQueryWorked { get; set; }
}

/// <summary>
/// Tests [FromQuery] complex object binding in AOT mode.
/// AOT ISSUE: [FromQuery] attribute discovery uses reflection.
/// Nested property binding uses recursive reflection.
/// Query parameter to complex type conversion uses TypeConverter.
/// </summary>
public class QueryParamBindingEndpoint : Endpoint<ComplexQueryRequest, ComplexQueryResponse>
{
    public override void Configure()
    {
        Get("complex-query-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ComplexQueryRequest req, CancellationToken ct)
    {
        var filter = req.Filter ?? new SearchFilter();

        await Send.OkAsync(new ComplexQueryResponse
        {
            Category = filter.Category,
            MinPrice = filter.MinPrice,
            MaxPrice = filter.MaxPrice,
            InStockOnly = filter.InStockOnly,
            TagCount = filter.Tags.Count,
            SortBy = req.SortBy,
            Ascending = req.Ascending,
            Page = req.Page,
            PageSize = req.PageSize,
            ComplexQueryWorked = !string.IsNullOrEmpty(filter.Category) || filter.MinPrice.HasValue
        });
    }
}
