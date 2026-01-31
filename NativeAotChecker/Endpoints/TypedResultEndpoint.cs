using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: TypedResults / IResult returning endpoints in AOT mode
public sealed class TypedResultRequest
{
    [QueryParam]
    public int StatusCode { get; set; } = 200;

    [QueryParam]
    public string Message { get; set; } = "Success";
}

public sealed class TypedResultResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public sealed class TypedResultEndpoint : Endpoint<TypedResultRequest, TypedResultResponse>
{
    public override void Configure()
    {
        Get("typed-result");
        AllowAnonymous();
        SerializerContext<TypedResultSerCtx>();
    }

    public override async Task HandleAsync(TypedResultRequest req, CancellationToken ct)
    {
        switch (req.StatusCode)
        {
            case 200:
                await Send.OkAsync(new TypedResultResponse
                {
                    Message = req.Message,
                    Timestamp = DateTime.UtcNow
                }, ct);
                break;
            case 201:
                await Send.CreatedAtAsync<TypedResultEndpoint>(
                    routeValues: null,
                    responseBody: new TypedResultResponse
                    {
                        Message = "Created: " + req.Message,
                        Timestamp = DateTime.UtcNow
                    }, 
                    cancellation: ct);
                break;
            case 204:
                await Send.NoContentAsync(ct);
                break;
            case 404:
                await Send.NotFoundAsync(ct);
                break;
            default:
                await Send.StatusCodeAsync(req.StatusCode, ct);
                break;
        }
    }
}

[JsonSerializable(typeof(TypedResultRequest))]
[JsonSerializable(typeof(TypedResultResponse))]
public partial class TypedResultSerCtx : JsonSerializerContext;
