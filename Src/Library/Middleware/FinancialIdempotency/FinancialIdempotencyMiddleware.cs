using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

sealed class FinancialIdempotencyMiddleware(RequestDelegate next, IFinancialIdempotencyStore store, ILogger<FinancialIdempotencyMiddleware> logger)
{
    internal static readonly object RejectionKey = new();
    internal static readonly object PipelineKey = new();

    internal const string ConflictMessage = "This idempotency key was already used with a different payload!";
    internal const string UnreplayableMessage = "The original response could not be cached for replay!";
    internal const string InvalidBeginMessage = "Financial idempotency could not start a reservation for this request.";
    internal const string InProgressMessage = "A request with this idempotency key is already in progress!";
    internal const string MultipleHeadersMessage = "Multiple idempotency headers not allowed!";

    public async Task Invoke(HttpContext ctx)
    {
        var opts = ctx.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>()?.FinancialIdempotencyOptions;

        if (opts is null)
        {
            await next(ctx);

            return;
        }

        ctx.Request.Headers.TryGetValue(opts.HeaderName, out var idmpKey);

        if (StringValues.IsNullOrEmpty(idmpKey))
        {
            await SendError(ctx, 400, $"Idempotency header [{opts.HeaderName}] is required!");

            return;
        }

        if (idmpKey.Count > 1)
        {
            await SendError(ctx, 400, MultipleHeadersMessage);

            return;
        }

        if (opts.AddHeaderToResponse)
            ctx.Response.Headers.TryAdd(opts.HeaderName, idmpKey);

        if (string.IsNullOrWhiteSpace(idmpKey[0]) || idmpKey[0]!.Length > opts.MaxKeyLength)
        {
            await SendError(ctx, 400, "Invalid idempotency key!");
            return;
        }

        var callerScope = opts.CallerScope?.Invoke(ctx);
        if (string.IsNullOrWhiteSpace(callerScope))
        {
            await SendError(ctx, 400, "A stable caller scope is required!");
            return;
        }

        var identityKey = FinancialIdentity.Build(ctx.Request, opts, callerScope);
        var payloadHash = await FinancialPayloadHash.ComputeAsync(ctx.Request, ctx.RequestAborted);
        var ttl = opts.Duration;

        var began = await store.TryBeginAsync(identityKey, payloadHash, ttl, ctx.RequestAborted);

        switch (began.Kind)
        {
            case FinancialBeginKind.Replay when began.Response is not null:
                await WriteReplay(ctx, opts, idmpKey, began.Response);

                return;
            case FinancialBeginKind.Replay:
                await SendError(ctx, 500, UnreplayableMessage);

                return;
            case FinancialBeginKind.Conflict:
                await SendError(ctx, 409, ConflictMessage);

                return;
            case FinancialBeginKind.Unreplayable:
                await SendError(ctx, 500, UnreplayableMessage);

                return;
            case FinancialBeginKind.InFlight:
                await SendError(ctx, 409, InProgressMessage);

                return;
            case FinancialBeginKind.Started when !string.IsNullOrWhiteSpace(began.ReservationToken):
                await ExecuteAndSettle(ctx, opts, identityKey, began.ReservationToken, ttl);

                return;
            case FinancialBeginKind.Started:
                await SendError(ctx, 500, InvalidBeginMessage);

                return;
            default:
                throw new InvalidOperationException($"Unknown financial idempotency begin kind: {began.Kind}");
        }
    }

    async Task ExecuteAndSettle(HttpContext ctx, FinancialIdempotencyOptions opts, string identityKey, string token, TimeSpan ttl)
    {
        var originalResponse = ctx.Features.Get<IHttpResponseFeature>()!;
        var originalBody = ctx.Features.Get<IHttpResponseBodyFeature>()!;
        await using var capture = new FinancialResponseCapture(originalResponse, opts.MaxResponseBodySize);
        ctx.Features.Set<IHttpResponseFeature>(capture);
        ctx.Features.Set<IHttpResponseBodyFeature>(capture);
        ctx.Items[RejectionKey] = false;
        ctx.Items[PipelineKey] = true;
        byte[] body;
        try
        {
            await next(ctx);

            // A finished handler must settle even if the client disconnected.
            if (!ReferenceEquals(ctx.Features.Get<IHttpResponseBodyFeature>(), capture) ||
                !ReferenceEquals(ctx.Features.Get<IHttpResponseFeature>(), capture))
                throw new InvalidOperationException("Financial response capture was replaced.");
            body = await capture.SnapshotAsync();
            await SettleReservation(ctx, opts, identityKey, token, ttl, capture, body);
        }
        catch
        {
            await MarkUnreplayableSafe(identityKey, token, opts.SettlementTimeout);
            throw;
        }
        finally
        {
            ctx.Features.Set(originalResponse);
            ctx.Features.Set(originalBody);
            ctx.Items.Remove(RejectionKey);
            ctx.Items.Remove(PipelineKey);
        }

        originalResponse.StatusCode = capture.StatusCode;
        originalResponse.ReasonPhrase = capture.ReasonPhrase;
        originalResponse.Headers.Clear();
        foreach (var header in capture.Headers)
            originalResponse.Headers[header.Key] = header.Value;
        await WriteBodyAsync(ctx, capture.StatusCode, body);
    }

    async Task SettleReservation(HttpContext ctx,
                                FinancialIdempotencyOptions opts,
                                string identityKey,
                                string token,
                                TimeSpan ttl,
                                FinancialResponseCapture capture,
                                byte[] body)
    {
        using var settlement = new CancellationTokenSource(opts.SettlementTimeout);

        if (capture.StatusCode is >= 200 and <= 299)
        {
            var result = await store.CompleteAsync(identityKey, token, new()
            {
                StatusCode = capture.StatusCode,
                Headers = FinancialIdempotencyHeaders.SnapshotHeaders(capture.Headers),
                Body = BodyAllowed(ctx, capture.StatusCode) ? body : []
            }, ttl, settlement.Token).WaitAsync(settlement.Token);

            if (result == FinancialSettlementResult.OwnershipLost)
                throw new InvalidOperationException("Financial reservation ownership was lost.");
        }
        else if (ctx.Items[RejectionKey] is true)
            await store.AbandonAsync(identityKey, token, settlement.Token).WaitAsync(settlement.Token);
        else
            await store.MarkUnreplayableAsync(identityKey, token, settlement.Token).WaitAsync(settlement.Token);
    }

    async Task MarkUnreplayableSafe(string identityKey, string token, TimeSpan timeout)
    {
        try
        {
            using var settlement = new CancellationTokenSource(timeout);
            await store.MarkUnreplayableAsync(identityKey, token, settlement.Token).WaitAsync(settlement.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Financial idempotency settlement failed; reservation retained.");
        }
    }

    static async Task WriteBodyAsync(HttpContext ctx, int statusCode, byte[] body)
    {
        if (BodyAllowed(ctx, statusCode))
        {
            ctx.Response.ContentLength = body.Length;
            await ctx.Response.Body.WriteAsync(body, ctx.RequestAborted);
        }
        else
            ctx.Response.ContentLength = null;
    }

    static bool BodyAllowed(HttpContext ctx, int status)
        => !HttpMethods.IsHead(ctx.Request.Method) && status is not (204 or 205 or 304) && status >= 200;

    static async Task WriteReplay(HttpContext ctx,
                                  FinancialIdempotencyOptions opts,
                                  StringValues idmpKey,
                                  FinancialIdempotencyResponse stored)
    {
        ctx.Response.StatusCode = opts.ReplayStatusCode ?? stored.StatusCode;

        foreach (var header in stored.Headers)
        {
            if (FinancialIdempotencyHeaders.IsHopByHop(header.Key))
                continue;

            ctx.Response.Headers[header.Key] = header.Value;
        }

        if (opts.AddHeaderToResponse)
            ctx.Response.Headers.TryAdd(opts.HeaderName, idmpKey);

        ctx.MarkResponseStart();
        await WriteBodyAsync(ctx, ctx.Response.StatusCode, stored.Body);
    }

    static Task SendError(HttpContext ctx, int statusCode, string message)
        => ctx.Response.SendErrorsAsync(
            [new(Cfg.ErrOpts.GeneralErrorsField, message)],
            statusCode);
}