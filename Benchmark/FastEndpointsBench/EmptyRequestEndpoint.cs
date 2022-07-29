using FastEndpoints;

namespace FEBench;

public class EmptyRequestEndpoint : Endpoint<EmptyRequest, object>
{
    public override void Configure()
    {
        Get("/empty-request");
        AllowAnonymous();
    }

    public override Task HandleAsync(EmptyRequest _, CancellationToken __)
    {
        HttpContext.Response.ContentLength = 27;
        return SendAsync(new { message = "Hello, World!" });
    }
}