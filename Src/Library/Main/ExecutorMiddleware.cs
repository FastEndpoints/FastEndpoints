using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal class ExecutorMiddleware
{
    private const string authInvoked = "__AuthorizationMiddlewareWithEndpointInvoked";
    private const string corsInvoked = "__CorsMiddlewareWithEndpointInvoked";
    private readonly IEndpointFactory _epFactory;
    private readonly RequestDelegate _next;

    public ExecutorMiddleware(IEndpointFactory epFactory, RequestDelegate next)
    {
        _epFactory = epFactory;
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task Invoke(HttpContext ctx)
    {
        var ep = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint;

        if (ep is null)
            return _next(ctx);

        var epDef = ep.Metadata.GetMetadata<EndpointDefinition>();

        if (epDef is null)
            return _next(ctx); //this is not a fastendpoint

        if (ep.Metadata.GetMetadata<IAuthorizeData>() != null && !ctx.Items.ContainsKey(authInvoked))
            ThrowAuthMiddlewareMissing(ep.DisplayName!);

        if (ep.Metadata.GetMetadata<ICorsMetadata>() != null && !ctx.Items.ContainsKey(corsInvoked))
            ThrowCORSMiddlewareMissing(ep.DisplayName!);

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

        var epInstance = _epFactory.Create(epDef, ctx);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;

        ResponseCacheExecutor.Execute(ctx, ep.Metadata.GetMetadata<ResponseCacheAttribute>());

        return epInstance.ExecAsync(ctx.RequestAborted);
    }

    private static void ThrowAuthMiddlewareMissing(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains authorization metadata, " +
            "but a middleware was not found that supports authorization." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseAuthorization() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints() the call to app.UseAuthorization() must go between them.");
    }

    private static void ThrowCORSMiddlewareMissing(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains CORS metadata, " +
            "but a middleware was not found that supports CORS." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseCors() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints(), the call to app.UseCors() must go between them.");
    }
}