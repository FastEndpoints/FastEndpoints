using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: EndpointWithoutRequest<TResponse> in AOT mode
public sealed class NoRequestResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
    public string ServerName { get; set; } = string.Empty;
}

public sealed class EndpointWithoutRequestEndpoint : EndpointWithoutRequest<NoRequestResponse>
{
    public override void Configure()
    {
        Get("no-request-endpoint");
        AllowAnonymous();
        SerializerContext<NoRequestSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new NoRequestResponse
        {
            Message = "Hello from endpoint without request!",
            ServerTime = DateTime.UtcNow,
            ServerName = Environment.MachineName
        }, ct);
    }
}

// Test: EndpointWithoutRequest (no response type) in AOT mode
public sealed class EndpointWithoutRequestNoResponseEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("no-request-no-response-endpoint");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.NoContentAsync(ct);
    }
}

[JsonSerializable(typeof(NoRequestResponse))]
public partial class NoRequestSerCtx : JsonSerializerContext;
