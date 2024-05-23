namespace TestCases.Idempotency;

sealed class Endpoint : EndpointWithoutRequest<Response>
{
    public static string BaseRoute { get; } = "api/test-cases/idempotency";

    public override void Configure()
    {
        Get($"{BaseRoute}/{{id}}");
        RoutePrefixOverride(string.Empty);
        AllowAnonymous();
        Idempotency(
            o =>
            {
                o.CacheDuration = TimeSpan.FromSeconds(10);
            });
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var id = Route<string>("id");
        await SendAsync(
            new()
            {
                Id = id ?? "",
                Ticks = DateTime.UtcNow.Ticks
            });
    }
}

sealed class Response
{
    public string Id { get; set; }
    public long Ticks { get; set; }
}