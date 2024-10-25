using FastEndpoints;

namespace Contracts;

sealed class ExtraRequest
{
    public int Age { get; set; }
    public string Name { get; set; }
}

sealed class MyEndpoint : Endpoint<ExtraRequest>
{
    public override void Configure()
    {
        Get("extra/{age}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExtraRequest req, CancellationToken ct)
    {
        await SendAsync(req);
    }
}