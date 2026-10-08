namespace TestCases.StreamStatusCodeTest;

public class Endpoint : EndpointWithoutRequest
{
    static readonly byte[] _payload = "partial-success"u8.ToArray();

    public override void Configure()
    {
        Get("/test-cases/stream-status/{mode}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        switch (Route<string>("mode"))
        {
            case "bytes":
                await Send.BytesAsync(_payload, 207, fileName: "result.txt", contentType: "text/plain", cancellation: ct);

                break;
            case "stream":
                await Send.StreamAsync(new MemoryStream(_payload), 207, fileName: "result.txt", contentType: "text/plain", cancellation: ct);

                break;
            case "file":
                var path = Path.GetTempFileName();
                await File.WriteAllBytesAsync(path, _payload, ct);

                try
                {
                    await Send.FileAsync(new FileInfo(path), 207, contentType: "text/plain", cancellation: ct);
                }
                finally
                {
                    File.Delete(path);
                }

                break;
            case "default":
                await Send.StreamAsync(new MemoryStream(_payload), fileName: "result.txt", contentType: "text/plain", cancellation: ct);

                break;
            case "range":
                await Send.BytesAsync(_payload, 207, contentType: "text/plain", enableRangeProcessing: true, cancellation: ct);

                break;
            default:
                await Send.NotFoundAsync(ct);

                break;
        }
    }
}
