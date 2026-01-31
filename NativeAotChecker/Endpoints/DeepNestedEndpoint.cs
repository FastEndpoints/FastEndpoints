using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Deeply nested object binding in AOT mode (4+ levels deep)
public sealed class Level4
{
    public string DeepValue { get; set; } = string.Empty;
    public int DeepNumber { get; set; }
}

public sealed class Level3
{
    public string Value3 { get; set; } = string.Empty;
    public Level4 Level4 { get; set; } = new();
}

public sealed class Level2
{
    public string Value2 { get; set; } = string.Empty;
    public Level3 Level3 { get; set; } = new();
}

public sealed class Level1
{
    public string Value1 { get; set; } = string.Empty;
    public Level2 Level2 { get; set; } = new();
}

public sealed class DeepNestedRequest
{
    public string RootValue { get; set; } = string.Empty;
    public Level1 Level1 { get; set; } = new();
}

public sealed class DeepNestedResponse
{
    public string RootValue { get; set; } = string.Empty;
    public string Level1Value { get; set; } = string.Empty;
    public string Level2Value { get; set; } = string.Empty;
    public string Level3Value { get; set; } = string.Empty;
    public string Level4Value { get; set; } = string.Empty;
    public int Level4Number { get; set; }
    public bool AllLevelsBound { get; set; }
}

public sealed class DeepNestedEndpoint : Endpoint<DeepNestedRequest, DeepNestedResponse>
{
    public override void Configure()
    {
        Post("deep-nested-test");
        AllowAnonymous();
        SerializerContext<DeepNestedSerCtx>();
    }

    public override async Task HandleAsync(DeepNestedRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DeepNestedResponse
        {
            RootValue = req.RootValue,
            Level1Value = req.Level1.Value1,
            Level2Value = req.Level1.Level2.Value2,
            Level3Value = req.Level1.Level2.Level3.Value3,
            Level4Value = req.Level1.Level2.Level3.Level4.DeepValue,
            Level4Number = req.Level1.Level2.Level3.Level4.DeepNumber,
            AllLevelsBound = !string.IsNullOrEmpty(req.RootValue) &&
                             !string.IsNullOrEmpty(req.Level1.Value1) &&
                             !string.IsNullOrEmpty(req.Level1.Level2.Value2) &&
                             !string.IsNullOrEmpty(req.Level1.Level2.Level3.Value3) &&
                             !string.IsNullOrEmpty(req.Level1.Level2.Level3.Level4.DeepValue)
        }, ct);
    }
}

[JsonSerializable(typeof(DeepNestedRequest))]
[JsonSerializable(typeof(DeepNestedResponse))]
[JsonSerializable(typeof(Level1))]
[JsonSerializable(typeof(Level2))]
[JsonSerializable(typeof(Level3))]
[JsonSerializable(typeof(Level4))]
public partial class DeepNestedSerCtx : JsonSerializerContext;
