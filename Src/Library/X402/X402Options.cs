using System.Text.Json.Nodes;

namespace FastEndpoints;

/// <summary>
/// x402 payment settings
/// </summary>
public sealed class X402Options
{
    /// <summary>
    /// whether x402 middleware should enforce payment protected endpoints
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// facilitator base url used for verify/settle requests
    /// </summary>
    public string FacilitatorUrl { get; set; } = string.Empty;

    /// <summary>
    /// settlement timing for paid requests
    /// </summary>
    public Settle SettlementMode { get; set; } = Settle.AfterSuccess;

    /// <summary>
    /// request timeout for facilitator calls
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// global defaults applied to payment protected endpoints
    /// </summary>
    public X402Defaults Defaults { get; } = new();

    internal void ThrowIfInvalid()
    {
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(FacilitatorUrl))
            throw new InvalidOperationException("x402 facilitator url has not been configured!");

        Defaults.ThrowIfIncomplete();
    }
}

/// <summary>
/// global x402 defaults
/// </summary>
public sealed class X402Defaults
{
    /// <summary>
    /// payment scheme. only 'exact' is currently supported.
    /// </summary>
    public string Scheme { get; set; } = X402Constants.ExactScheme;

    /// <summary>
    /// caip-2 network identifier
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// receiving wallet address
    /// </summary>
    public string? PayTo { get; set; }

    /// <summary>
    /// asset/token identifier expected by the facilitator
    /// </summary>
    public string? Asset { get; set; }

    /// <summary>
    /// max timeout in seconds advertised to buyers
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// default resource mime type
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// optional extra metadata included in the payment requirements
    /// </summary>
    public JsonObject? Extra { get; set; }

    internal void ThrowIfIncomplete()
    {
        if (!string.Equals(Scheme, X402Constants.ExactScheme, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("only the 'exact' x402 scheme is currently supported!");

        if (string.IsNullOrWhiteSpace(Network))
            throw new InvalidOperationException("x402 default network has not been configured!");

        if (string.IsNullOrWhiteSpace(PayTo))
            throw new InvalidOperationException("x402 default pay-to address has not been configured!");

        if (string.IsNullOrWhiteSpace(Asset))
            throw new InvalidOperationException("x402 default asset has not been configured!");
    }
}

/// <summary>
/// endpoint-level x402 options
/// </summary>
public sealed class X402EndpointOptions
{
    /// <summary>
    /// price for 'exact' payment scheme. can be $ style or facilitator-specific string.
    /// </summary>
    public string Price { get; internal set; } = null!;

    /// <summary>
    /// short resource description shown to buyers
    /// </summary>
    public string Description { get; internal set; } = null!;

    /// <summary>
    /// payment scheme. only 'exact' is currently supported.
    /// </summary>
    public string? Scheme { get; set; }

    /// <summary>
    /// caip-2 network identifier override
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// recipient address override
    /// </summary>
    public string? PayTo { get; set; }

    /// <summary>
    /// asset/token identifier override
    /// </summary>
    public string? Asset { get; set; }

    /// <summary>
    /// resource mime type override
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// max timeout override
    /// </summary>
    public int? MaxTimeoutSeconds { get; set; }

    /// <summary>
    /// settlement timing override for this endpoint
    /// </summary>
    public Settle? SettlementMode { get; set; }

    /// <summary>
    /// additional payment requirement metadata
    /// </summary>
    public JsonObject? Extra { get; set; }

    internal X402PaymentMetadata ToMetadata()
    {
        if (string.IsNullOrWhiteSpace(Price))
            throw new InvalidOperationException("x402 price is required!");

        if (string.IsNullOrWhiteSpace(Description))
            throw new InvalidOperationException("x402 description is required!");

        return new()
        {
            Scheme = Scheme,
            Price = Price,
            Description = Description,
            Network = Network,
            PayTo = PayTo,
            Asset = Asset,
            MimeType = MimeType,
            MaxTimeoutSeconds = MaxTimeoutSeconds,
            SettlementMode = SettlementMode,
            Extra = Extra?.DeepClone().AsObject()
        };
    }
}

/// <summary>
/// determines when settlement occurs for verified x402 requests
/// </summary>
public enum Settle
{
    /// <summary>
    /// verify payment, execute handler, then settle only after a successful response.
    /// </summary>
    AfterSuccess,

    /// <summary>
    /// verify and settle before executing the endpoint handler.
    /// </summary>
    BeforeHandler
}