using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal static class RequestHandler
{
    internal static Task Invoke(HttpContext ctx, IEndpointFactory epFactory)
    {
        var epMeta = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!).Endpoint!.Metadata;
        var epDef = epMeta.GetMetadata<EndpointDefinition>()!;

        //note: epMeta and epDef will never be null since this method is only invoked for fastendpoints

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

        if (PrepAndCheckAcceptsMetaData(ctx, epDef, epMeta))
        {
            ctx.Response.StatusCode = 415;
            return ctx.Response.StartAsync(ctx.RequestAborted);
        }

        var epInstance = epFactory.Create(epDef, ctx);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;
        ctx.Items[CtxKey.ValidationFailures] = epInstance.ValidationFailures;

        ResponseCacheExecutor.Execute(ctx, epDef.ResponseCacheSettings);

        return epInstance.ExecAsync(ctx.RequestAborted);
    }

    private static bool PrepAndCheckAcceptsMetaData(HttpContext ctx, EndpointDefinition def, EndpointMetadataCollection epMeta)
    {
        if (def.AcceptsMetaDataPresent is null) //only ever iterating the meta collection once on first request
        {
            def.AcceptsMetaDataPresent = false;

            for (var i = 0; i < epMeta.Count; i++)
            {
                if (epMeta[i] is IAcceptsMetadata meta)
                {
                    def.AcceptsMetaDataPresent = true;
                    def.AcceptsAnyContentType = meta.ContentTypes.Contains("*/*");
                }
            }
        }

        // if following conditions are met:
        //   1.) request doesn't contain any content-type headers
        //   2.) endpoint declares accepts metadata
        //   3.) endpoint doesn't declare any wildcard accepts metadata
        // then return true so that a 415 response can be sent to the client.
        // we don't need to check for mismatched content-types (between request and endpoint)
        // because routing middleware already takes care of that.
        return !ctx.Request.Headers.ContainsKey(HeaderNames.ContentType) &&
                def.AcceptsMetaDataPresent is true &&
               !def.AcceptsAnyContentType;
    }
}