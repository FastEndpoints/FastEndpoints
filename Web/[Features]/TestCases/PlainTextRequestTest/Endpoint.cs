namespace TestCases.PlainTextRequestTest;

public class Request : PlainTextRequest
{
    public int Id { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Post("test-cases/plaintext/{Id}");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        return SendAsync(req); //mirror back the request dto
    }
}
