using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Enum binding from query/route parameters in AOT mode
public enum ProductCategory
{
    Electronics,
    Clothing,
    Food,
    Books,
    Other
}

public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public sealed class EnumBindingRequest
{
    [RouteParam]
    public ProductCategory Category { get; set; }

    [QueryParam]
    public OrderStatus Status { get; set; }

    [QueryParam]
    public ProductCategory? OptionalCategory { get; set; }
}

public sealed class EnumBindingResponse
{
    public ProductCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public ProductCategory? OptionalCategory { get; set; }
    public string? OptionalCategoryName { get; set; }
}

public sealed class EnumBindingEndpoint : Endpoint<EnumBindingRequest, EnumBindingResponse>
{
    public override void Configure()
    {
        Get("enum-binding/{category}");
        AllowAnonymous();
        SerializerContext<EnumBindingSerCtx>();
    }

    public override async Task HandleAsync(EnumBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new EnumBindingResponse
        {
            Category = req.Category,
            CategoryName = req.Category.ToString(),
            Status = req.Status,
            StatusName = req.Status.ToString(),
            OptionalCategory = req.OptionalCategory,
            OptionalCategoryName = req.OptionalCategory?.ToString()
        }, ct);
    }
}

[JsonSerializable(typeof(EnumBindingRequest))]
[JsonSerializable(typeof(EnumBindingResponse))]
public partial class EnumBindingSerCtx : JsonSerializerContext;
