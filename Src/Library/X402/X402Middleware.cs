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
        var requirements = resolved.ToPaymentRequirements(ctx);

        if (!ctx.Request.Headers.TryGetValue(X402Constants.PaymentSignatureHeader, out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements, cancellation: ctx.RequestAborted);

            return;
        }

        PaymentPayload payload;

        try
        {
            payload = X402Serializer.FromBase64<PaymentPayload>(signature!);
        }
        catch
        {
            await SendPaymentRequiredAsync(ctx, resolved, requirements, "Invalid payment payload", ctx.RequestAborted);

            return;
        }

        var facilitator = ctx.RequestServices.GetRequiredService<IX402FacilitatorClient>();
        var verifyRequest = new VerificationRequest { PaymentPayload = payload, PaymentRequirements = requirements };
        var verification = await facilitator.VerifyAsync(verifyRequest, ctx.RequestAborted);

        if (!verification.IsValid)
        {
            if (verification.PaymentResponse is not null)
                ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(verification.PaymentResponse);

            await SendPaymentRequiredAsync(ctx, resolved, requirements, verification.InvalidReason, ctx.RequestAborted);

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
                await SendPaymentRequiredAsync(ctx, resolved, requirements, settlement.Error, ctx.RequestAborted);

                return;
            }

            ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(AttachRequirements(settlement, requirements));
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
                await SendPaymentRequiredAsync(ctx, resolved, requirements, settlement.Error, ctx.RequestAborted);

                return;
            }

            ctx.Response.Headers[X402Constants.PaymentResponseHeader] = X402Serializer.ToBase64(AttachRequirements(settlement, requirements));
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
            throw new InvalidOperationException("only the 'exact' x402 scheme is currently supported!");

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
            Extra = Merge(defaults.Extra, metadata.Extra)
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

    static SettlementResponse AttachRequirements(SettlementResponse response, PaymentRequirements requirements)
        => response.Requirements is null
               ? new()
               {
                   Success = response.Success,
                   Transaction = response.Transaction,
                   Network = response.Network,
                   Payer = response.Payer,
                   Error = response.Error,
                   Requirements = requirements
               }
               : response;

    static async Task SendPaymentRequiredAsync(HttpContext ctx,
                                               X402ResolvedPaymentConfig config,
                                               PaymentRequirements requirements,
                                               string? error = null,
                                               CancellationToken cancellation = default)
    {
        var paymentRequired = new PaymentRequiredResponse
        {
            Error = string.IsNullOrWhiteSpace(error) ? "Payment required" : error,
            Resource = new()
            {
                Url = ctx.Request.GetDisplayUrl(),
                Description = config.Description,
                MimeType = config.MimeType
            },
            Accepts = [requirements]
        };

        ctx.Response.StatusCode = 402;
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers[X402Constants.PaymentRequiredHeader] = X402Serializer.ToBase64(paymentRequired);
        await ctx.Response.StartAsync(cancellation);
    }
}