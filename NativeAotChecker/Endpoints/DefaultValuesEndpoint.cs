using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Default values in request DTOs in AOT mode
public sealed class DefaultValuesRequest
{
    public string Name { get; set; } = "DefaultName";
    public int Count { get; set; } = 10;
    public bool IsEnabled { get; set; } = true;
    public double Rate { get; set; } = 0.5;
    public List<string> Tags { get; set; } = ["default", "tag"];
    public ProductCategory Category { get; set; } = ProductCategory.Electronics;
}

public sealed class DefaultValuesResponse
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsEnabled { get; set; }
    public double Rate { get; set; }
    public List<string> Tags { get; set; } = [];
    public ProductCategory Category { get; set; }
    public bool DefaultsPreserved { get; set; }
}

public sealed class DefaultValuesEndpoint : Endpoint<DefaultValuesRequest, DefaultValuesResponse>
{
    public override void Configure()
    {
        Post("default-values-test");
        AllowAnonymous();
        SerializerContext<DefaultValuesSerCtx>();
    }

    public override async Task HandleAsync(DefaultValuesRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DefaultValuesResponse
        {
            Name = req.Name,
            Count = req.Count,
            IsEnabled = req.IsEnabled,
            Rate = req.Rate,
            Tags = req.Tags,
            Category = req.Category,
            DefaultsPreserved = req.Name == "DefaultName" && 
                                req.Count == 10 && 
                                req.IsEnabled == true
        }, ct);
    }
}

[JsonSerializable(typeof(DefaultValuesRequest))]
[JsonSerializable(typeof(DefaultValuesResponse))]
[JsonSerializable(typeof(ProductCategory))]
public partial class DefaultValuesSerCtx : JsonSerializerContext;
