using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Send various HTTP status codes in AOT mode
public sealed class StatusCodeRequest
{
    [QueryParam]
    public int Code { get; set; } = 200;
}

public sealed class StatusCodeResponse
{
    public int RequestedCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class SendStatusCodeEndpoint : Endpoint<StatusCodeRequest, StatusCodeResponse>
{
    public override void Configure()
    {
        Get("send-status-code");
        AllowAnonymous();
        SerializerContext<StatusCodeSerCtx>();
    }

    public override async Task HandleAsync(StatusCodeRequest req, CancellationToken ct)
    {
        switch (req.Code)
        {
            case 200:
                await Send.OkAsync(new StatusCodeResponse
                {
                    RequestedCode = 200,
                    Message = "OK"
                }, ct);
                break;
            case 201:
                await Send.CreatedAtAsync<SendStatusCodeEndpoint>(
                    routeValues: null,
                    responseBody: new StatusCodeResponse
                    {
                        RequestedCode = 201,
                        Message = "Created"
                    },
                    cancellation: ct);
                break;
            case 202:
                await Send.StatusCodeAsync(202, ct);
                break;
            case 204:
                await Send.NoContentAsync(ct);
                break;
            case 400:
                AddError("Bad request intentional error");
                await Send.ErrorsAsync(400, ct);
                break;
            case 404:
                await Send.NotFoundAsync(ct);
                break;
            case 500:
                await Send.StatusCodeAsync(500, ct);
                break;
            default:
                await Send.StatusCodeAsync(req.Code, ct);
                break;
        }
    }
}

[JsonSerializable(typeof(StatusCodeRequest))]
[JsonSerializable(typeof(StatusCodeResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class StatusCodeSerCtx : JsonSerializerContext;
