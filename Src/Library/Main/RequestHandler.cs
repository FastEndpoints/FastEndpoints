using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

static class RequestHandler
{
    internal static Task Invoke(HttpContext ctx, IEndpointFactory epFactory)
    {
        var epDef = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!).Endpoint!.Metadata.GetMetadata<EndpointDefinition>()!;

        if (epDef.HitCounter is not null)
        {
            var hdrName = epDef.HitCounter.HeaderName ?? ThrOpts.HeaderName ?? "X-Forwarded-For";

            if (!ctx.Request.Headers.TryGetValue(hdrName, out var hdrVal))
            {
                hdrVal = ctx.Connection.RemoteIpAddress?.ToString();

                if (hdrVal.Count == 0)
                {
                    ctx.Response.StatusCode = 403;

                    return ctx.Response.WriteAsync("Forbidden by rate limiting middleware!", ctx.RequestAborted);
                }
            }

            if (epDef.HitCounter.LimitReached(hdrVal[0]!))
            {
                ctx.Response.StatusCode = 429;

                return ctx.Response.WriteAsync(ThrOpts.Message ?? "You are requesting this endpoint too frequently!", ctx.RequestAborted);
            }
        }

        if (!ctx.Request.Headers.ContainsKey(HeaderNames.ContentType) && epDef is { AcceptsMetaDataPresent: true, AcceptsAnyContentType: false })
        {
            // if all 3 conditions are true:
            //   1.) request doesn't contain any content-type headers
            //   2.) endpoint declares accepts metadata
            //   3.) endpoint doesn't declare wildcard accepts metadata
            // then a 415 response needs to be sent to the client.
            // we don't need to check for mismatched content-types (between request and endpoint)
            // because routing middleware already takes care of that.

            ctx.Response.StatusCode = 415;

            return ctx.Response.StartAsync(ctx.RequestAborted);
        }

        var epInstance = epFactory.Create(epDef, ctx);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;
        ctx.Items[CtxKey.ValidationFailures] = epInstance.ValidationFailures;
        ctx.Items[CtxKey.ToHeaderProps] = epDef.ToHeaderProps;

        ResponseCacheExecutor.Execute(ctx, epDef.ResponseCacheSettings);

        return epInstance.ExecAsync(ctx.RequestAborted);
    }
}