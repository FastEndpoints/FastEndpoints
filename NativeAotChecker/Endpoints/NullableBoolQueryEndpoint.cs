using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

/// <summary>
/// Test: Nullable bool query parameter binding in AOT mode.
/// AOT ISSUE: Nullable value type (bool?) binding from query string fails.
/// Non-nullable bool works fine, but bool? causes 500 error.
/// </summary>
public sealed class NullableBoolQueryRequest
{
    [QueryParam]
    public bool NonNullableBool { get; set; }

    [QueryParam]
    public bool? NullableBool { get; set; }
}

public sealed class NullableBoolQueryResponse
{
    public bool NonNullableBool { get; set; }
    public bool? NullableBool { get; set; }
}

public sealed class NullableBoolQueryEndpoint : Endpoint<NullableBoolQueryRequest, NullableBoolQueryResponse>
{
    public override void Configure()
    {
        Get("nullable-bool-query-test");
        AllowAnonymous();
        SerializerContext<NullableBoolQuerySerCtx>();
    }

    public override async Task HandleAsync(NullableBoolQueryRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new NullableBoolQueryResponse
        {
            NonNullableBool = req.NonNullableBool,
            NullableBool = req.NullableBool
        });
    }
}

[JsonSerializable(typeof(NullableBoolQueryRequest))]
[JsonSerializable(typeof(NullableBoolQueryResponse))]
public partial class NullableBoolQuerySerCtx : JsonSerializerContext;
