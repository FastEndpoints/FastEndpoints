using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request/Response DTOs for expression-based routing
public class ExpressionRouteRequest
{
    public int OrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ExpressionRouteResponse
{
    public int OrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool ExpressionRouteWorked { get; set; }
    public string ComputedRoute { get; set; } = string.Empty;
}

/// <summary>
/// Tests expression-based route pattern in AOT mode.
/// AOT ISSUE: Expression<Func<TRequest, object>> uses expression tree compilation.
/// Route parameter extraction from lambda expression uses reflection.
/// BuildRoute() method compiles expression at runtime.
/// </summary>
public class ExpressionRouteEndpoint : Endpoint<ExpressionRouteRequest, ExpressionRouteResponse>
{
    public override void Configure()
    {
        // Using expression-based route pattern
        Get("order/{@id}/product/{@code}", r => new { r.OrderId, r.ProductCode });
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExpressionRouteRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ExpressionRouteResponse
        {
            OrderId = req.OrderId,
            ProductCode = req.ProductCode,
            Quantity = req.Quantity,
            ExpressionRouteWorked = true,
            ComputedRoute = $"order/{req.OrderId}/product/{req.ProductCode}"
        });
    }
}
