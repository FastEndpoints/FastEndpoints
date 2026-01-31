using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Response caching configuration in AOT mode
public sealed class CachedResponse
{
    public string Data { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string UniqueId { get; set; } = string.Empty;
}

public sealed class ResponseCachingEndpoint : EndpointWithoutRequest<CachedResponse>
{
    public override void Configure()
    {
        Get("cached-endpoint");
        AllowAnonymous();
        ResponseCache(60); // Cache for 60 seconds
        SerializerContext<ResponseCachingSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new CachedResponse
        {
            Data = "This response should be cached",
            GeneratedAt = DateTime.UtcNow,
            UniqueId = Guid.NewGuid().ToString()
        }, ct);
    }
}

// Test: No-cache endpoint
public sealed class NoCacheEndpoint : EndpointWithoutRequest<CachedResponse>
{
    public override void Configure()
    {
        Get("no-cache-endpoint");
        AllowAnonymous();
        SerializerContext<ResponseCachingSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new CachedResponse
        {
            Data = "This response should NOT be cached",
            GeneratedAt = DateTime.UtcNow,
            UniqueId = Guid.NewGuid().ToString()
        }, ct);
    }
}

[JsonSerializable(typeof(CachedResponse))]
public partial class ResponseCachingSerCtx : JsonSerializerContext;
