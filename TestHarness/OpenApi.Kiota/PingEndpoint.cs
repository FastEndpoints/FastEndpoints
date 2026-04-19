using FastEndpoints;

namespace OpenApi.Kiota;

public sealed class PingEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/api/ping");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("pong", ct);
}
