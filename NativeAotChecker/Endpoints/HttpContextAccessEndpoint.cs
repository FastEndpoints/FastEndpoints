using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: HttpContext access in endpoint in AOT mode
public sealed class HttpContextAccessResponse
{
    public string RequestPath { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? UserAgent { get; set; }
    public string? Host { get; set; }
    public bool IsHttps { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public int? LocalPort { get; set; }
    public bool HttpContextAccessible { get; set; }
}

public sealed class HttpContextAccessEndpoint : EndpointWithoutRequest<HttpContextAccessResponse>
{
    public override void Configure()
    {
        Get("http-context-access-test");
        AllowAnonymous();
        SerializerContext<HttpContextAccessSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ctx = HttpContext;
        
        await Send.OkAsync(new HttpContextAccessResponse
        {
            RequestPath = ctx.Request.Path,
            RequestMethod = ctx.Request.Method,
            ContentType = ctx.Request.ContentType,
            UserAgent = ctx.Request.Headers.UserAgent.ToString(),
            Host = ctx.Request.Host.Value,
            IsHttps = ctx.Request.IsHttps,
            Scheme = ctx.Request.Scheme,
            LocalPort = ctx.Connection.LocalPort,
            HttpContextAccessible = true
        }, ct);
    }
}

[JsonSerializable(typeof(HttpContextAccessResponse))]
public partial class HttpContextAccessSerCtx : JsonSerializerContext;
