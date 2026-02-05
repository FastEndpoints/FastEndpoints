using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.KnownAotIssues;

// Test: BindFrom attribute for property name aliasing in AOT mode
public sealed class BindFromRequest
{
    [BindFrom("customer_id")]
    public int CustomerId { get; set; }

    [BindFrom("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [BindFrom("qty")]
    public int Quantity { get; set; }

    [QueryParam]
    [BindFrom("cat")]
    public string Category { get; set; } = string.Empty;
}

public sealed class BindFromResponse
{
    public int CustomerId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool AllBindingsWorked { get; set; }
}

public sealed class BindFromEndpoint : Endpoint<BindFromRequest, BindFromResponse>
{
    public override void Configure()
    {
        Post("bind-from-test");
        AllowAnonymous();
        SerializerContext<BindFromSerCtx>();
    }

    public override async Task HandleAsync(BindFromRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new BindFromResponse
        {
            CustomerId = req.CustomerId,
            ProductName = req.ProductName,
            Quantity = req.Quantity,
            Category = req.Category,
            AllBindingsWorked = req.CustomerId > 0 && 
                                !string.IsNullOrEmpty(req.ProductName) &&
                                req.Quantity > 0
        }, ct);
    }
}

[JsonSerializable(typeof(BindFromRequest))]
[JsonSerializable(typeof(BindFromResponse))]
public partial class BindFromSerCtx : JsonSerializerContext;
