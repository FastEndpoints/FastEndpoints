using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Custom value types with TryParse in AOT mode
public readonly struct CustomId : IParsable<CustomId>
{
    public string Prefix { get; }
    public int Number { get; }

    public CustomId(string prefix, int number)
    {
        Prefix = prefix;
        Number = number;
    }

    public static CustomId Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;
        throw new FormatException($"Cannot parse '{s}' as CustomId");
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out CustomId result)
    {
        result = default;
        if (string.IsNullOrEmpty(s) || !s.Contains('-'))
            return false;

        var parts = s.Split('-', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            return false;

        result = new CustomId(parts[0], number);
        return true;
    }

    public override string ToString() => $"{Prefix}-{Number}";
}

public sealed class CustomValueParserRequest
{
    public CustomId Id { get; set; }
    
    [QueryParam]
    public CustomId? OptionalId { get; set; }
}

public sealed class CustomValueParserResponse
{
    public string IdPrefix { get; set; } = string.Empty;
    public int IdNumber { get; set; }
    public string? OptionalIdPrefix { get; set; }
    public int? OptionalIdNumber { get; set; }
    public bool CustomParserWorked { get; set; }
}

public sealed class CustomValueParserEndpoint : Endpoint<CustomValueParserRequest, CustomValueParserResponse>
{
    public override void Configure()
    {
        Get("custom-value-parser/{Id}");
        AllowAnonymous();
        SerializerContext<CustomValueParserSerCtx>();
    }

    public override async Task HandleAsync(CustomValueParserRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new CustomValueParserResponse
        {
            IdPrefix = req.Id.Prefix,
            IdNumber = req.Id.Number,
            OptionalIdPrefix = req.OptionalId?.Prefix,
            OptionalIdNumber = req.OptionalId?.Number,
            CustomParserWorked = !string.IsNullOrEmpty(req.Id.Prefix) && req.Id.Number > 0
        }, ct);
    }
}

[JsonSerializable(typeof(CustomValueParserRequest))]
[JsonSerializable(typeof(CustomValueParserResponse))]
public partial class CustomValueParserSerCtx : JsonSerializerContext;
