using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Nullable types binding in AOT mode
public sealed class NullableTypesRequest
{
    public int? NullableInt { get; set; }
    public double? NullableDouble { get; set; }
    public bool? NullableBool { get; set; }
    public DateTime? NullableDateTime { get; set; }
    public Guid? NullableGuid { get; set; }
    public ProductCategory? NullableEnum { get; set; }
    public string? NullableString { get; set; }

    [QueryParam]
    public int? QueryNullableInt { get; set; }
}

public sealed class NullableTypesResponse
{
    public int? NullableInt { get; set; }
    public double? NullableDouble { get; set; }
    public bool? NullableBool { get; set; }
    public DateTime? NullableDateTime { get; set; }
    public Guid? NullableGuid { get; set; }
    public ProductCategory? NullableEnum { get; set; }
    public string? NullableString { get; set; }
    public int? QueryNullableInt { get; set; }
    public int NullCount { get; set; }
    public int NotNullCount { get; set; }
}

public sealed class NullableTypesEndpoint : Endpoint<NullableTypesRequest, NullableTypesResponse>
{
    public override void Configure()
    {
        Post("nullable-types");
        AllowAnonymous();
        SerializerContext<NullableTypesSerCtx>();
    }

    public override async Task HandleAsync(NullableTypesRequest req, CancellationToken ct)
    {
        var nullCount = 0;
        var notNullCount = 0;

        void Count(object? val)
        {
            if (val == null) nullCount++;
            else notNullCount++;
        }

        Count(req.NullableInt);
        Count(req.NullableDouble);
        Count(req.NullableBool);
        Count(req.NullableDateTime);
        Count(req.NullableGuid);
        Count(req.NullableEnum);
        Count(req.NullableString);
        Count(req.QueryNullableInt);

        await Send.OkAsync(new NullableTypesResponse
        {
            NullableInt = req.NullableInt,
            NullableDouble = req.NullableDouble,
            NullableBool = req.NullableBool,
            NullableDateTime = req.NullableDateTime,
            NullableGuid = req.NullableGuid,
            NullableEnum = req.NullableEnum,
            NullableString = req.NullableString,
            QueryNullableInt = req.QueryNullableInt,
            NullCount = nullCount,
            NotNullCount = notNullCount
        }, ct);
    }
}

[JsonSerializable(typeof(NullableTypesRequest))]
[JsonSerializable(typeof(NullableTypesResponse))]
[JsonSerializable(typeof(ProductCategory))]
public partial class NullableTypesSerCtx : JsonSerializerContext;
