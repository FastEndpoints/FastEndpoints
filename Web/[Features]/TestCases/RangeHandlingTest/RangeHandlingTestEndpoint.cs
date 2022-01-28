using System.Text;

namespace TestCases.RangeHandlingTest;

public class Endpoint : EndpointWithoutRequest
{
    private static readonly byte[] content = Encoding.UTF8.GetBytes("abcdefghijklmnopqwstuvwxyz");

    public override void Configure()
    {
        Get("/test-cases/range");
        AllowAnonymous();
        Options(o => o.Produces<string>(206, "text/plain"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendBytesAsync(content, contentType: "text/plain", enableRangeProcessing: true);
    }
}