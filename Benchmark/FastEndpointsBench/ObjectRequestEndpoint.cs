using FastEndpoints;

namespace FEBench;

public class ObjectRequestEndpoint : Endpoint<object, object>
{
    public override void Configure()
    {
        Get("/object-request");
        AllowAnonymous();
    }

    public override Task HandleAsync(object _, CancellationToken __)
    {
        HttpContext.Response.ContentLength = 27;
        return SendAsync(new { message = "Hello, World!" });
    }
}