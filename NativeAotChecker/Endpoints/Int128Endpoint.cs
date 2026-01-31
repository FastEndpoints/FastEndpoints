using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with large numeric types
public class LargeNumericRequest
{
    public Int128 LargeInt { get; set; }
    public UInt128 LargeUInt { get; set; }
    public Half HalfPrecision { get; set; }
    public decimal BigDecimal { get; set; }
}

public class LargeNumericResponse
{
    public string LargeIntString { get; set; } = string.Empty;
    public string LargeUIntString { get; set; } = string.Empty;
    public double HalfAsDouble { get; set; }
    public decimal BigDecimal { get; set; }
    public bool LargeNumericWorked { get; set; }
}

/// <summary>
/// Tests Int128, UInt128, and Half numeric types in AOT mode.
/// AOT ISSUE: Int128/UInt128 are not natively supported by JSON serialization.
/// Half precision requires custom converter.
/// Large number serialization needs string representation fallback.
/// </summary>
public class LargeNumericEndpoint : Endpoint<LargeNumericRequest, LargeNumericResponse>
{
    public override void Configure()
    {
        Post("large-numeric-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LargeNumericRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new LargeNumericResponse
        {
            LargeIntString = req.LargeInt.ToString(),
            LargeUIntString = req.LargeUInt.ToString(),
            HalfAsDouble = (double)req.HalfPrecision,
            BigDecimal = req.BigDecimal,
            LargeNumericWorked = true
        });
    }
}
