using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request with [DontBind] attribute
public class DontBindRequest
{
    /// <summary>
    /// This property won't be bound from query or route
    /// </summary>
    [DontBind(Source.QueryParam | Source.RouteParam)]
    public string InternalId { get; set; } = "default-internal";

    /// <summary>
    /// Normal binding from all sources
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// Query parameter only binding
    /// </summary>
    [QueryParam]
    public string QueryOnly { get; set; } = string.Empty;

    /// <summary>
    /// Route parameter only binding
    /// </summary>
    [RouteParam]
    public string RouteOnly { get; set; } = string.Empty;
}

public class DontBindResponse
{
    public string InternalId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string QueryOnly { get; set; } = string.Empty;
    public string RouteOnly { get; set; } = string.Empty;
    public bool DontBindWorked { get; set; }
}

/// <summary>
/// Tests [DontBind] and source-specific binding attributes in AOT mode.
/// AOT ISSUE: [DontBind], [QueryParam], [RouteParam] discovery uses reflection.
/// Binding source filtering uses enum flags at runtime.
/// Per-property binding source configuration uses reflection.
/// </summary>
public class DontBindEndpoint : Endpoint<DontBindRequest, DontBindResponse>
{
    public override void Configure()
    {
        Get("dont-bind-test/{RouteOnly}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DontBindRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DontBindResponse
        {
            InternalId = req.InternalId,
            ExternalId = req.ExternalId,
            QueryOnly = req.QueryOnly,
            RouteOnly = req.RouteOnly,
            DontBindWorked = req.InternalId == "default-internal" // Should keep default value
        });
    }
}
