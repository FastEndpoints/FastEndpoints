using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace FastEndpoints;

sealed class X402Middleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var metadata = ctx.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>()?.X402PaymentMetadata;

        if (metadata is null)
        {
            await next(ctx);

            return;
        }

        var resolved = Resolve(metadata);
        var requirements = resolved.ToPaymentRequirements();

        if (!ctx.Request.Headers.TryGetValue(X402Constants.PaymentSignatureHeader, out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements);

            return;
        }

        PaymentPayload payload;

        try
        {
            payload = X402Serializer.FromBase64<PaymentPayload>(signature!);
        }
        catch
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements, "Invalid payment payload!");

            return;
        }

        var facilitator = ctx.RequestServices.GetRequiredService<IX402FacilitatorClient>();

        if (TryValidatePayload(payload, requirements, out var validationError))
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements, validationError);

            return;
        }

        var verifyRequest = new VerificationRequest { PaymentPayload = payload, PaymentRequirements = requirements };
        var verification = await facilitator.VerifyAsync(verifyRequest, ctx.RequestAborted);

        if (!verification.IsValid)
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements, verification.InvalidReason);

            return;
        }

        var requestContext = new X402RequestContext
        {
            PaymentPayload = payload,
            PaymentRequirements = requirements,
            VerificationResult = verification
        };

        if (GetSettlementMode(metadata) == Settle.BeforeHandler)
        {
            var settlement = await facilitator.SettleAsync(new() { PaymentPayload = payload, PaymentRequirements = requirements }, ctx.RequestAborted);
            requestContext.SettlementResult = settlement;
            ctx.SetX402RequestContext(requestContext);

            if (!settlement.Success)
            {
                ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(settlement);
                await SendPaymentRequiredAsync(ctx, resolved, requirements, settlement.ErrorReason);

                return;
            }

            ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(settlement);
            await next(ctx);

            return;
        }

        ctx.SetX402RequestContext(requestContext);

        var originalBody = ctx.Response.Body;
        await using var bufferedBody = new BufferedResponseStream(originalBody);
        ctx.Response.Body = bufferedBody;

        try
        {
            await next(ctx);

            if (ctx.Response.StatusCode >= 400)
            {
                await bufferedBody.CopyToInnerAsync(ctx.RequestAborted);

                return;
            }

            var settlement = await facilitator.SettleAsync(new() { PaymentPayload = payload, PaymentRequirements = requirements }, ctx.RequestAborted);
            requestContext.SettlementResult = settlement;

            if (!settlement.Success)
            {
                ctx.Response.Clear();
                ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(settlement);
                await SendPaymentRequiredAsync(ctx, resolved, requirements, settlement.ErrorReason);

                return;
            }

            ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(settlement);
            await bufferedBody.CopyToInnerAsync(ctx.RequestAborted);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }
    }

    static X402ResolvedPaymentConfig Resolve(X402PaymentMetadata metadata)
    {
        var defaults = Cfg.X402Opts.Defaults;
        defaults.ThrowIfIncomplete();

        var scheme = metadata.Scheme ?? defaults.Scheme;

        if (!string.Equals(scheme, X402Constants.ExactScheme, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the 'exact' x402 scheme is currently supported!");

        return new()
        {
            Scheme = scheme,
            Price = metadata.Price,
            Description = metadata.Description,
            Network = metadata.Network ?? defaults.Network!,
            PayTo = metadata.PayTo ?? defaults.PayTo!,
            Asset = metadata.Asset ?? defaults.Asset!,
            MimeType = metadata.MimeType ?? defaults.MimeType,
            MaxTimeoutSeconds = metadata.MaxTimeoutSeconds ?? defaults.MaxTimeoutSeconds,
            Extra = Merge(defaults.Extra, metadata.Extra),
            Extensions = Merge(defaults.Extensions, metadata.Extensions)
        };
    }

    static Settle GetSettlementMode(X402PaymentMetadata metadata)
        => metadata.SettlementMode ?? Cfg.X402Opts.SettlementMode;

    static JsonObject? Merge(JsonObject? left, JsonObject? right)
    {
        if (left is null && right is null)
            return null;

        var merged = left?.DeepClone().AsObject() ?? [];

        if (right is null)
            return merged;

        foreach (var kvp in right)
            merged[kvp.Key] = kvp.Value?.DeepClone();

        return merged;
    }

    static bool TryValidatePayload(PaymentPayload payload, PaymentRequirements requirements, out string? error)
    {
        error = null;

        if (payload.X402Version != X402Constants.Version)
        {
            error = "invalid_x402_version";

            return true;
        }

        if (!MatchesRequirements(payload.Accepted, requirements))
        {
            error = "invalid_payment_requirements";

            return true;
        }

        if (payload.Payload is null || !payload.Payload.TryGetPropertyValue("authorization", out var authorizationNode) || authorizationNode is null)
            return false;

        if (authorizationNode is not JsonObject authorization ||
            !TryGetString(authorization, "to", out var to) ||
            !TryGetString(authorization, "value", out var value) ||
            !TryGetString(authorization, "validAfter", out var validAfterRaw) ||
            !TryGetString(authorization, "validBefore", out var validBeforeRaw) ||
            !long.TryParse(validAfterRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var validAfter) ||
            !long.TryParse(validBeforeRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var validBefore))
        {
            error = "invalid_payload";

            return true;
        }

        if (!string.Equals(to, requirements.PayTo, StringComparison.Ordinal))
        {
            error = "invalid_exact_evm_payload_recipient_mismatch";

            return true;
        }

        if (!string.Equals(value, requirements.Amount, StringComparison.Ordinal))
        {
            error = "invalid_exact_evm_payload_authorization_value_mismatch";

            return true;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (now < validAfter)
        {
            error = "invalid_exact_evm_payload_authorization_valid_after";

            return true;
        }

        if (now > validBefore)
        {
            error = "invalid_exact_evm_payload_authorization_valid_before";

            return true;
        }

        return false;
    }

    static bool MatchesRequirements(PaymentRequirements accepted, PaymentRequirements required)
        => string.Equals(accepted.Scheme, required.Scheme, StringComparison.Ordinal) &&
           string.Equals(accepted.Network, required.Network, StringComparison.Ordinal) &&
           string.Equals(accepted.Amount, required.Amount, StringComparison.Ordinal) &&
           string.Equals(accepted.Asset, required.Asset, StringComparison.Ordinal) &&
           string.Equals(accepted.PayTo, required.PayTo, StringComparison.Ordinal) &&
           accepted.MaxTimeoutSeconds == required.MaxTimeoutSeconds;

    static bool TryGetString(JsonObject obj, string propertyName, out string? value)
    {
        value = null;

        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is null)
            return false;

        value = node.GetValue<string>();

        return true;
    }

    static async Task SendPaymentRequiredAsync(HttpContext ctx, X402ResolvedPaymentConfig config, PaymentRequirements requirements, string? error = null)
    {
        var paymentRequired = new PaymentRequiredResponse
        {
            Error = string.IsNullOrWhiteSpace(error) ? "Payment required!" : error,
            Resource = new()
            {
                Url = ctx.Request.GetDisplayUrl(),
                Description = config.Description,
                MimeType = config.MimeType
            },
            Accepts = [requirements],
            Extensions = config.Extensions?.DeepClone().AsObject()
        };

        ctx.Response.StatusCode = 402;
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers[X402Constants.PaymentRequiredHeader] = X402Serializer.ToBase64(paymentRequired);
        await ctx.Response.StartAsync(ctx.RequestAborted);
    }
}