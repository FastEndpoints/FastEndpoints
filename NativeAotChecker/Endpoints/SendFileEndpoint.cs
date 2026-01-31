using System.Text;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Send file/bytes response in AOT mode
public sealed class SendFileRequest
{
    [QueryParam]
    public string FileName { get; set; } = "test.txt";

    [QueryParam]
    public string Content { get; set; } = "Hello from AOT!";

    [QueryParam]
    public string ContentType { get; set; } = "text/plain";
}

public sealed class SendFileEndpoint : Endpoint<SendFileRequest>
{
    public override void Configure()
    {
        Get("send-file-test");
        AllowAnonymous();
        SerializerContext<SendFileSerCtx>();
    }

    public override async Task HandleAsync(SendFileRequest req, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(req.Content);
        await Send.BytesAsync(bytes, req.FileName, req.ContentType, cancellation: ct);
    }
}

// Test: Send stream response in AOT mode
public sealed class SendStreamRequest
{
    [QueryParam]
    public string Data { get; set; } = "Stream data content";
}

public sealed class SendStreamEndpoint : Endpoint<SendStreamRequest>
{
    public override void Configure()
    {
        Get("send-stream-test");
        AllowAnonymous();
        SerializerContext<SendFileSerCtx>();
    }

    public override async Task HandleAsync(SendStreamRequest req, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(req.Data);
        var stream = new MemoryStream(bytes);
        await Send.StreamAsync(stream, "stream.txt", stream.Length, "application/octet-stream", cancellation: ct);
    }
}

[JsonSerializable(typeof(SendFileRequest))]
[JsonSerializable(typeof(SendStreamRequest))]
public partial class SendFileSerCtx : JsonSerializerContext;
