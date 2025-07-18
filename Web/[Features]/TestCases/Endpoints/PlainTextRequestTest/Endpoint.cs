﻿namespace TestCases.PlainTextRequestTest;

public class Request : PlainTextRequest
{
    /// <summary>
    /// id of the plain text request
    /// </summary>
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
        RoutePrefixOverride(string.Empty);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "plain request endpoint summary";
            s.RequestParam(r => r.Id, "overriden id text");
            s.RequestParam(r => r.Content, "overriden content text");
        });
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        Response = new()
        {
            Id = req.Id,
            BodyContent = req.Content
        };

        return Send.ResponseAsync(Response);
    }
}