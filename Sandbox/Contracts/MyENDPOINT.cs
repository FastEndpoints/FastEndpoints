using FastEndpoints;

namespace Contracts;

public sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("MYMYMYM");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await SendAsync("ok");
    }
}