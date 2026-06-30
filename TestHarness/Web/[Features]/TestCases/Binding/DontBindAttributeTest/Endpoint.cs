namespace TestCases.DontBindAttributeTest;

sealed class Request
{
    public int Id { get; set; }

    [DontBind(Source.RouteParam | Source.QueryParam)]
    public string Name { get; set; }
}

sealed class Response
{
    public string Result { get; set; }
}

sealed class Endpoint : Endpoint<Request, string>
{
    public override void Configure()
    {
        Post("test-cases/dont-bind-attribute-test/{Name}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await Send.StringAsync($"{r.Id} - {r.Name}");
    }
}