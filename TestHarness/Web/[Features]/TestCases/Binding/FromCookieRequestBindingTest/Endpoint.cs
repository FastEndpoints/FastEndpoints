namespace TestCases.FromCookieRequestBindingTest;

sealed class Request
{
    [FromCookie]
    public string SomeCookie { get; set; }

    public Guid Id { get; set; }
}

sealed class Response
{
    public Guid Id { get; set; }
    public string CookieValue { get; set; }
}

sealed class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/test-cases/from-cookie-binding-test/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await Send.OkAsync(
            new()
            {
                Id = r.Id,
                CookieValue = r.SomeCookie
            });
    }
}