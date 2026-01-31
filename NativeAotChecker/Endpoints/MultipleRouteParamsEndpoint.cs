using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Route binding with multiple route parameters in AOT mode
public sealed class MultipleRouteParamsRequest
{
    public string Category { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Variant { get; set; } = string.Empty;
    
    [QueryParam]
    public string? Filter { get; set; }
}

public sealed class MultipleRouteParamsResponse
{
    public string Category { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Variant { get; set; } = string.Empty;
    public string? Filter { get; set; }
    public bool AllParamsBound { get; set; }
}

public sealed class MultipleRouteParamsEndpoint : Endpoint<MultipleRouteParamsRequest, MultipleRouteParamsResponse>
{
    public override void Configure()
    {
        Get("category/{Category}/product/{ProductId}/variant/{Variant}");
        AllowAnonymous();
        SerializerContext<MultipleRouteParamsSerCtx>();
    }

    public override async Task HandleAsync(MultipleRouteParamsRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new MultipleRouteParamsResponse
        {
            Category = req.Category,
            ProductId = req.ProductId,
            Variant = req.Variant,
            Filter = req.Filter,
            AllParamsBound = !string.IsNullOrEmpty(req.Category) && 
                             req.ProductId > 0 && 
                             !string.IsNullOrEmpty(req.Variant)
        }, ct);
    }
}

[JsonSerializable(typeof(MultipleRouteParamsRequest))]
[JsonSerializable(typeof(MultipleRouteParamsResponse))]
public partial class MultipleRouteParamsSerCtx : JsonSerializerContext;
