namespace TestCases.StronglyTypedRouteParamTest;

sealed class Request
{
    [BindFrom("platformKitUid")]
    public string Uid { get; set; }

    public string Name { get; set; }
}

sealed class MyEndpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/test-cases/strong-route-params/{@id}/blah/{@nom}", x => new { x.Uid, x.Name });
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await Send.OkAsync(r);
    }
}