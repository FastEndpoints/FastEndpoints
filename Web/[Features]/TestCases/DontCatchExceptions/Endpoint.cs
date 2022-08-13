namespace TestCases.DontCatchExceptions;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        DontCatchExceptions();
        Get("test-cases/{number}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendStringAsync(r.Number.ToString());
    }
}

public class Request
{
    public int Number { get; set; }
}