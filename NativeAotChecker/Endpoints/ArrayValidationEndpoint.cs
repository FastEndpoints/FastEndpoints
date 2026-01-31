using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Object array and List validation in AOT mode
public sealed class ArrayObjectItem
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class ArrayValidationRequest
{
    public List<ArrayObjectItem> Items { get; set; } = [];
    public int[] Numbers { get; set; } = [];
    public string[] Strings { get; set; } = [];
}

public sealed class ArrayValidationResponse
{
    public int ItemCount { get; set; }
    public int NumberCount { get; set; }
    public int StringCount { get; set; }
    public int TotalSum { get; set; }
    public string AllNames { get; set; } = string.Empty;
}

public sealed class ArrayValidationEndpoint : Endpoint<ArrayValidationRequest, ArrayValidationResponse>
{
    public override void Configure()
    {
        Post("array-validation");
        AllowAnonymous();
        SerializerContext<ArrayValidationSerCtx>();
    }

    public override async Task HandleAsync(ArrayValidationRequest req, CancellationToken ct)
    {
        // Manually validate array items
        for (var i = 0; i < req.Items.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(req.Items[i].Name))
            {
                AddError($"Items[{i}].Name", "Name is required for each item");
            }
        }

        ThrowIfAnyErrors();

        await Send.OkAsync(new ArrayValidationResponse
        {
            ItemCount = req.Items.Count,
            NumberCount = req.Numbers.Length,
            StringCount = req.Strings.Length,
            TotalSum = req.Items.Sum(x => x.Value) + req.Numbers.Sum(),
            AllNames = string.Join(", ", req.Items.Select(x => x.Name))
        }, ct);
    }
}

[JsonSerializable(typeof(ArrayObjectItem))]
[JsonSerializable(typeof(ArrayValidationRequest))]
[JsonSerializable(typeof(ArrayValidationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class ArrayValidationSerCtx : JsonSerializerContext;
