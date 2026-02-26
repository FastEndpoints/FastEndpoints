using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.Binding;

public sealed class BindFromRequest
{
    [JsonPropertyName("customer_id")] // for STJ we need to use JsonPropertyName
    public int CustomerId { get; set; }

    [BindFrom("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("qty")] // for STJ we need to use JsonPropertyName
    public int Quantity { get; set; }

    [QueryParam, BindFrom("cat")]
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
    }

    public override async Task HandleAsync(BindFromRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                CustomerId = req.CustomerId,
                ProductName = req.ProductName,
                Quantity = req.Quantity,
                Category = req.Category,
                AllBindingsWorked = req.CustomerId > 0 &&
                                    !string.IsNullOrEmpty(req.ProductName) &&
                                    req.Quantity > 0
            },
            ct);
    }
}