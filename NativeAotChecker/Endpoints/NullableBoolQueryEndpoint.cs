using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

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
        await Send.OkAsync(
            new()
            {
                NonNullableBool = req.NonNullableBool,
                NullableBool = req.NullableBool
            });
    }
}

[JsonSerializable(typeof(NullableBoolQueryResponse)), JsonSerializable(typeof(ErrorResponse))]
public partial class NullableBoolQuerySerCtx : JsonSerializerContext;