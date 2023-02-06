using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal static class RequestHandler
{
    public static Task Invoke(HttpContext ctx, IEndpointFactory epFactory)
    {
        var ep = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint;
        var epDef = ep?.Metadata.GetMetadata<EndpointDefinition>();

        if (epDef is null || ep is null)
            throw new InvalidOperationException($"Unable to retrieve endpoint definition for route: [{ctx.Request.Path}]");

        if (epDef.HitCounter is not null)
        {
            var hdrName = epDef.HitCounter.HeaderName ?? ThrOpts.HeaderName ?? "X-Forwarded-For";

            if (!ctx.Request.Headers.TryGetValue(hdrName, out var hdrVal))
            {
                hdrVal = ctx.Connection.RemoteIpAddress?.ToString();

                if (hdrVal.Count == 0)
                {
                    ctx.Response.StatusCode = 403;
                    return ctx.Response.WriteAsync("Forbidden by rate limiting middleware!");
                }
            }

            if (epDef.HitCounter.LimitReached(hdrVal[0]!))
            {
                ctx.Response.StatusCode = 429;
                return ctx.Response.WriteAsync(ThrOpts.Message ?? "You are requesting this endpoint too frequently!");
            }
        }

        if (!ctx.Request.Headers.ContainsKey(HeaderNames.ContentType) &&
             ep.Metadata.OfType<IAcceptsMetadata>().Any() &&
            !ep.Metadata.OfType<IAcceptsMetadata>().Any(m => m.ContentTypes.Contains("*/*")))
        {
            ctx.Response.StatusCode = 415;
            return ctx.Response.StartAsync();
        }

        var epInstance = epFactory.Create(epDef, ctx);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;

        ResponseCacheExecutor.Execute(ctx, ep.Metadata.GetMetadata<ResponseCacheAttribute>());

        return epInstance.ExecAsync(ctx.RequestAborted);
    }
}