namespace NativeAotChecker.Endpoints.Binding;

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
    }

    public override async Task HandleAsync(FromHeaderRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                CorrelationId = req.CorrelationId,
                TenantId = req.TenantId,
                ContentType = req.ContentType,
                OptionalHeader = req.OptionalHeader,
                AllHeadersBound = !string.IsNullOrEmpty(req.CorrelationId) &&
                                  !string.IsNullOrEmpty(req.TenantId)
            },
            ct);
    }
}