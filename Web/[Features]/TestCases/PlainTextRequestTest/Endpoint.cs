namespace TestCases.PlainTextRequestTest;

public class Request : PlainTextRequest
{
    public int Id { get; set; }
}

public class Response
{
    public int Id { get; set; }
    public string BodyContent { get; set; }
}

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("test-cases/plaintext/{Id}");
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        Response = new()
        {
            Id = req.Id,
            BodyContent = req.Content
        };

        return SendAsync(Response);
    }
}

