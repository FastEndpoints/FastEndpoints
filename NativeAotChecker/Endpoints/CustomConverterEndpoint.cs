using FastEndpoints;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Custom JsonConverter
public class CustomDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return DateTime.TryParseExact(value, Format, null, System.Globalization.DateTimeStyles.None, out var result)
            ? result
            : DateTime.Parse(value ?? DateTime.UtcNow.ToString());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}

// Request with custom converter attribute
public class CustomConverterRequest
{
    public string Name { get; set; } = string.Empty;
    
    [JsonConverter(typeof(CustomDateTimeConverter))]
    public DateTime CustomDate { get; set; }
    
    public DateTime StandardDate { get; set; }
}

// Response
public class CustomConverterResponse
{
    public string Name { get; set; } = string.Empty;
    
    [JsonConverter(typeof(CustomDateTimeConverter))]
    public DateTime CustomDate { get; set; }
    
    public DateTime StandardDate { get; set; }
    public string CustomDateFormatted { get; set; } = string.Empty;
    public bool CustomConverterWorked { get; set; }
}

/// <summary>
/// Tests [JsonConverter] attribute on properties in AOT mode.
/// AOT ISSUE: Property-level JsonConverter attribute uses reflection for discovery.
/// Custom converter instantiation uses Activator.CreateInstance.
/// Converter registration per-property needs runtime type inspection.
/// </summary>
public class CustomConverterEndpoint : Endpoint<CustomConverterRequest, CustomConverterResponse>
{
    public override void Configure()
    {
        Post("custom-converter-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CustomConverterRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new CustomConverterResponse
        {
            Name = req.Name,
            CustomDate = req.CustomDate,
            StandardDate = req.StandardDate,
            CustomDateFormatted = req.CustomDate.ToString("yyyy-MM-dd HH:mm:ss"),
            CustomConverterWorked = req.CustomDate != default
        });
    }
}
