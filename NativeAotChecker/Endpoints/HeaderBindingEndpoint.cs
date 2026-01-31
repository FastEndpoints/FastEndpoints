using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Header binding with FromHeader attribute in AOT mode
public sealed class HeaderBindingRequest
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

public sealed class HeaderBindingResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? OptionalHeader { get; set; }
    public bool AllHeadersBound { get; set; }
}

public sealed class HeaderBindingEndpoint : Endpoint<HeaderBindingRequest, HeaderBindingResponse>
{
    public override void Configure()
    {
        Get("header-binding");
        AllowAnonymous();
        SerializerContext<HeaderBindingSerCtx>();
    }

    public override async Task HandleAsync(HeaderBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new HeaderBindingResponse
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

[JsonSerializable(typeof(HeaderBindingRequest))]
[JsonSerializable(typeof(HeaderBindingResponse))]
public partial class HeaderBindingSerCtx : JsonSerializerContext;
