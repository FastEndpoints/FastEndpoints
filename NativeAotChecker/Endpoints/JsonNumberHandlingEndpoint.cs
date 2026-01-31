using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with JsonNumberHandling attribute
public class JsonNumberHandlingRequest
{
    // Allow reading numbers from strings
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int IntFromString { get; set; }
    
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double DoubleFromString { get; set; }
    
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal DecimalFromString { get; set; }
    
    // Write as string
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long LongAsString { get; set; }
    
    // Both directions
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int BothWays { get; set; }
}

public class JsonNumberHandlingResponse
{
    public int IntFromString { get; set; }
    public double DoubleFromString { get; set; }
    public decimal DecimalFromString { get; set; }
    
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long LongAsString { get; set; }
    
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public int BothWays { get; set; }
    
    public bool NumberHandlingWorked { get; set; }
}

/// <summary>
/// Tests [JsonNumberHandling] attribute in AOT mode.
/// AOT ISSUE: JsonNumberHandling attribute uses reflection for property discovery.
/// Number parsing with custom handling needs runtime attribute inspection.
/// Combined flags evaluation requires reflection-based attribute analysis.
/// </summary>
public class JsonNumberHandlingEndpoint : Endpoint<JsonNumberHandlingRequest, JsonNumberHandlingResponse>
{
    public override void Configure()
    {
        Post("json-number-handling-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonNumberHandlingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonNumberHandlingResponse
        {
            IntFromString = req.IntFromString,
            DoubleFromString = req.DoubleFromString,
            DecimalFromString = req.DecimalFromString,
            LongAsString = req.LongAsString,
            BothWays = req.BothWays,
            NumberHandlingWorked = req.IntFromString > 0 && req.DoubleFromString > 0
        });
    }
}
