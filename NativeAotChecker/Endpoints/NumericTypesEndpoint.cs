using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Decimal and other numeric types binding in AOT mode
public sealed class NumericTypesRequest
{
    public decimal DecimalValue { get; set; }
    public float FloatValue { get; set; }
    public double DoubleValue { get; set; }
    public long LongValue { get; set; }
    public short ShortValue { get; set; }
    public byte ByteValue { get; set; }
    public uint UIntValue { get; set; }
    public ulong ULongValue { get; set; }
    
    [QueryParam]
    public decimal? QueryDecimal { get; set; }
}

public sealed class NumericTypesResponse
{
    public decimal DecimalValue { get; set; }
    public float FloatValue { get; set; }
    public double DoubleValue { get; set; }
    public long LongValue { get; set; }
    public short ShortValue { get; set; }
    public byte ByteValue { get; set; }
    public uint UIntValue { get; set; }
    public ulong ULongValue { get; set; }
    public decimal? QueryDecimal { get; set; }
    public decimal Sum { get; set; }
    public bool AllNumericsBound { get; set; }
}

public sealed class NumericTypesEndpoint : Endpoint<NumericTypesRequest, NumericTypesResponse>
{
    public override void Configure()
    {
        Post("numeric-types-test");
        AllowAnonymous();
        SerializerContext<NumericTypesSerCtx>();
    }

    public override async Task HandleAsync(NumericTypesRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new NumericTypesResponse
        {
            DecimalValue = req.DecimalValue,
            FloatValue = req.FloatValue,
            DoubleValue = req.DoubleValue,
            LongValue = req.LongValue,
            ShortValue = req.ShortValue,
            ByteValue = req.ByteValue,
            UIntValue = req.UIntValue,
            ULongValue = req.ULongValue,
            QueryDecimal = req.QueryDecimal,
            Sum = req.DecimalValue + (decimal)req.FloatValue + (decimal)req.DoubleValue,
            AllNumericsBound = req.DecimalValue != 0 || req.LongValue != 0 || req.DoubleValue != 0
        }, ct);
    }
}

[JsonSerializable(typeof(NumericTypesRequest))]
[JsonSerializable(typeof(NumericTypesResponse))]
public partial class NumericTypesSerCtx : JsonSerializerContext;
