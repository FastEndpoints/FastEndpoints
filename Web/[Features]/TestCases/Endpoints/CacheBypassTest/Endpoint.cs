namespace TestCases.Endpoints.CacheBypassTest;

sealed class Request
{
    public Guid Id { get; set; }
}

sealed class Endpoint : Endpoint<Request, Guid>
{
    public override void Configure()
    {
        Get("test-cases/cache-bypass-test");
        AllowAnonymous();
        ResponseCache(60);
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendAsync(r.Id);
    }
}