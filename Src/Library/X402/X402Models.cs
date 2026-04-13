using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace FastEndpoints;

public sealed class X402PaymentMetadata
{
    public string? Scheme { get; init; }
    public string Price { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string? Network { get; init; }
    public string? PayTo { get; init; }
    public string? Asset { get; init; }
    public string? MimeType { get; init; }
    public int? MaxTimeoutSeconds { get; init; }
    public Settle? SettlementMode { get; init; }
    public JsonObject? Extra { get; init; }
}

public sealed class X402ResolvedPaymentConfig
{
    public required string Scheme { get; init; }
    public required string Price { get; init; }
    public required string Description { get; init; }
    public required string Network { get; init; }
    public required string PayTo { get; init; }
    public required string Asset { get; init; }
    public required int MaxTimeoutSeconds { get; init; }
    public string? MimeType { get; init; }
    public JsonObject? Extra { get; init; }

    internal PaymentRequirements ToPaymentRequirements(HttpContext ctx)
    {
        var extra = Extra?.DeepClone().AsObject() ?? [];
        extra["resourceUrl"] ??= ctx.Request.GetDisplayUrl();

        return new()
        {
            Scheme = Scheme,
            Network = Network,
            Amount = Price,
            Asset = Asset,
            PayTo = PayTo,
            MaxTimeoutSeconds = MaxTimeoutSeconds,
            Extra = extra
        };
    }
}

public sealed class PaymentRequiredResponse
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; init; } = X402Constants.Version;

    [JsonPropertyName("error")]
    public string Error { get; init; } = "Payment required";

    [JsonPropertyName("resource")]
    public required X402Resource Resource { get; init; }

    [JsonPropertyName("accepts")]
    public required List<PaymentRequirements> Accepts { get; init; }
}

public sealed class X402Resource
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

public sealed class PaymentRequirements
{
    [JsonPropertyName("scheme")]
    public required string Scheme { get; init; }

    [JsonPropertyName("network")]
    public required string Network { get; init; }

    [JsonPropertyName("amount")]
    public required string Amount { get; init; }

    [JsonPropertyName("asset")]
    public required string Asset { get; init; }

    [JsonPropertyName("payTo")]
    public required string PayTo { get; init; }

    [JsonPropertyName("maxTimeoutSeconds")]
    public required int MaxTimeoutSeconds { get; init; }

    [JsonPropertyName("extra")]
    public JsonObject? Extra { get; init; }
}

public sealed class PaymentPayload
{
    [JsonPropertyName("x402Version")]
    public int X402Version { get; init; }

    [JsonPropertyName("resource")]
    public required X402Resource Resource { get; init; }

    [JsonPropertyName("accepted")]
    public required PaymentRequirements Accepted { get; init; }

    [JsonPropertyName("payload")]
    public JsonObject? Payload { get; init; }
}

public sealed class VerificationRequest
{
    [JsonPropertyName("paymentPayload")]
    public required PaymentPayload PaymentPayload { get; init; }

    [JsonPropertyName("paymentRequirements")]
    public required PaymentRequirements PaymentRequirements { get; init; }
}

public sealed class VerificationResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("invalidReason")]
    public string? InvalidReason { get; init; }

    [JsonPropertyName("payer")]
    public string? Payer { get; init; }

    [JsonPropertyName("paymentResponse")]
    public SettlementResponse? PaymentResponse { get; init; }
}

public sealed class SettlementRequest
{
    [JsonPropertyName("paymentPayload")]
    public required PaymentPayload PaymentPayload { get; init; }

    [JsonPropertyName("paymentRequirements")]
    public required PaymentRequirements PaymentRequirements { get; init; }
}

public sealed class SettlementResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("transaction")]
    public string? Transaction { get; init; }

    [JsonPropertyName("network")]
    public string? Network { get; init; }

    [JsonPropertyName("payer")]
    public string? Payer { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("requirements")]
    public PaymentRequirements? Requirements { get; init; }
}

sealed class X402RequestContext
{
    public required PaymentPayload PaymentPayload { get; init; }
    public required PaymentRequirements PaymentRequirements { get; init; }
    public VerificationResponse? VerificationResult { get; init; }
    public SettlementResponse? SettlementResult { get; set; }
}