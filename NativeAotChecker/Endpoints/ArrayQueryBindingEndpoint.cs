using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Array of complex objects in query string in AOT mode
public sealed class QueryItem
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class ArrayQueryBindingRequest
{
    [QueryParam]
    public List<int> Ids { get; set; } = [];

    [QueryParam]
    public List<string> Names { get; set; } = [];

    [QueryParam]
    public int[] Numbers { get; set; } = [];

    [QueryParam]
    public string[]? OptionalStrings { get; set; }
}

public sealed class ArrayQueryBindingResponse
{
    public int IdCount { get; set; }
    public int NameCount { get; set; }
    public int NumberCount { get; set; }
    public int? OptionalStringCount { get; set; }
    public int FirstId { get; set; }
    public string? FirstName { get; set; }
    public bool ArraysBound { get; set; }
}

public sealed class ArrayQueryBindingEndpoint : Endpoint<ArrayQueryBindingRequest, ArrayQueryBindingResponse>
{
    public override void Configure()
    {
        Get("array-query-binding-test");
        AllowAnonymous();
        SerializerContext<ArrayQueryBindingSerCtx>();
    }

    public override async Task HandleAsync(ArrayQueryBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ArrayQueryBindingResponse
        {
            IdCount = req.Ids.Count,
            NameCount = req.Names.Count,
            NumberCount = req.Numbers.Length,
            OptionalStringCount = req.OptionalStrings?.Length,
            FirstId = req.Ids.FirstOrDefault(),
            FirstName = req.Names.FirstOrDefault(),
            ArraysBound = req.Ids.Count > 0 || req.Names.Count > 0 || req.Numbers.Length > 0
        }, ct);
    }
}

[JsonSerializable(typeof(ArrayQueryBindingRequest))]
[JsonSerializable(typeof(ArrayQueryBindingResponse))]
public partial class ArrayQueryBindingSerCtx : JsonSerializerContext;
