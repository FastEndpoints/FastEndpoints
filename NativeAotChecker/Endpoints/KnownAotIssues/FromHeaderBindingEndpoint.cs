using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.KnownAotIssues;

/// <summary>
/// Tests [FromHeader] attribute binding for HTTP headers in Native AOT mode.
/// AOT Issue: [FromHeader] binding uses reflection via GetCustomAttribute&lt;FromHeaderAttribute&gt;().
/// This metadata is trimmed in AOT, causing header values to not bind (empty string returned).
/// </summary>
public sealed class FromHeaderRequest
{
    [FromHeader("x-correlation-id")]
    public string CorrelationId { get; set; } = string.Empty;

    [FromHeader("x-tenant-id")]
    public string TenantId { get; set; } = string.Empty;

    [FromHeader("content-type")]
    public string ContentType { get; set; } = string.Empty;

    [FromHeader("x-optional-header", IsRequired = false)]
    public string? OptionalHeader { get; set; }
}

public sealed class FromHeaderResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? OptionalHeader { get; set; }
    public bool AllHeadersBound { get; set; }
}

public sealed class FromHeaderBindingEndpoint : Endpoint<FromHeaderRequest, FromHeaderResponse>
{
    public override void Configure()
    {
        Get("aot/from-header-binding");
        AllowAnonymous();
        RequestBinder(new RequestBinder<FromHeaderRequest>(BindingSource.Headers | BindingSource.QueryParams));
        SerializerContext<FromHeaderSerializerContext>();
    }

    public override async Task HandleAsync(FromHeaderRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new FromHeaderResponse
        {
            CorrelationId = req.CorrelationId,
            TenantId = req.TenantId,
            ContentType = req.ContentType,
            OptionalHeader = req.OptionalHeader,
            AllHeadersBound = !string.IsNullOrEmpty(req.CorrelationId) &&
                              !string.IsNullOrEmpty(req.TenantId)
        }, ct);
    }
}

[JsonSerializable(typeof(FromHeaderRequest))]
[JsonSerializable(typeof(FromHeaderResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class FromHeaderSerializerContext : JsonSerializerContext;
