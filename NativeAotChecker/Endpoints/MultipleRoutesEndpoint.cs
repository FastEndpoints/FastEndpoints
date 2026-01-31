using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Multiple routes for same endpoint in AOT mode
public sealed class MultiRouteRequest
{
    [QueryParam]
    public string Source { get; set; } = string.Empty;
}

public sealed class MultiRouteResponse
{
    public string Route { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class MultipleRoutesEndpoint : Endpoint<MultiRouteRequest, MultiRouteResponse>
{
    public override void Configure()
    {
        Get("multi-route-1", "multi-route-2", "multi-route-3");
        AllowAnonymous();
        SerializerContext<MultiRouteSerCtx>();
    }

    public override async Task HandleAsync(MultiRouteRequest req, CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value ?? "unknown";

        await Send.OkAsync(new MultiRouteResponse
        {
            Route = path,
            Source = req.Source,
            Message = $"Request came through route: {path}"
        }, ct);
    }
}

// Test: Routes() method with multiple routes
public sealed class RoutesMethodEndpoint : Endpoint<MultiRouteRequest, MultiRouteResponse>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("routes-method-a", "routes-method-b");
        AllowAnonymous();
        SerializerContext<MultiRouteSerCtx>();
    }

    public override async Task HandleAsync(MultiRouteRequest req, CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value ?? "unknown";

        await Send.OkAsync(new MultiRouteResponse
        {
            Route = path,
            Source = req.Source,
            Message = $"Routes() method endpoint: {path}"
        }, ct);
    }
}

[JsonSerializable(typeof(MultiRouteRequest))]
[JsonSerializable(typeof(MultiRouteResponse))]
public partial class MultiRouteSerCtx : JsonSerializerContext;
