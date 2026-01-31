using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Different HTTP verbs (PUT, DELETE, PATCH) in AOT mode
public sealed class HttpVerbRequest
{
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

public sealed class HttpVerbResponse
{
    public string HttpMethod { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

// PUT endpoint
public sealed class PutVerbEndpoint : Endpoint<HttpVerbRequest, HttpVerbResponse>
{
    public override void Configure()
    {
        Put("http-verbs/{id}");
        AllowAnonymous();
        SerializerContext<HttpVerbSerCtx>();
    }

    public override async Task HandleAsync(HttpVerbRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new HttpVerbResponse
        {
            HttpMethod = "PUT",
            Id = req.Id,
            Data = req.Data,
            ProcessedAt = DateTime.UtcNow
        }, ct);
    }
}

// DELETE endpoint
public sealed class DeleteVerbEndpoint : Endpoint<HttpVerbRequest, HttpVerbResponse>
{
    public override void Configure()
    {
        Delete("http-verbs/{id}");
        AllowAnonymous();
        SerializerContext<HttpVerbSerCtx>();
    }

    public override async Task HandleAsync(HttpVerbRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new HttpVerbResponse
        {
            HttpMethod = "DELETE",
            Id = req.Id,
            Data = "Deleted",
            ProcessedAt = DateTime.UtcNow
        }, ct);
    }
}

// PATCH endpoint
public sealed class PatchVerbEndpoint : Endpoint<HttpVerbRequest, HttpVerbResponse>
{
    public override void Configure()
    {
        Patch("http-verbs/{id}");
        AllowAnonymous();
        SerializerContext<HttpVerbSerCtx>();
    }

    public override async Task HandleAsync(HttpVerbRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new HttpVerbResponse
        {
            HttpMethod = "PATCH",
            Id = req.Id,
            Data = req.Data,
            ProcessedAt = DateTime.UtcNow
        }, ct);
    }
}

// HEAD endpoint (returns no body)
public sealed class HeadVerbEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Head("http-verbs-head");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers["X-Custom-Header"] = "HeadResponse";
        await Send.NoContentAsync(ct);
    }
}

[JsonSerializable(typeof(HttpVerbRequest))]
[JsonSerializable(typeof(HttpVerbResponse))]
public partial class HttpVerbSerCtx : JsonSerializerContext;
