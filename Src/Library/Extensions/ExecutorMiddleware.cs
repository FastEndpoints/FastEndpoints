using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
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
        var endpoint = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint;

        if (endpoint is null) return _next(ctx);

        var epDef = endpoint.Metadata.GetMetadata<EndpointDefinition>();

        if (epDef is not null)
        {
            if (endpoint.Metadata.GetMetadata<IAuthorizeData>() != null && !ctx.Items.ContainsKey(authInvoked))
                ThrowAuthMiddlewareMissing(endpoint.DisplayName!);

            if (endpoint.Metadata.GetMetadata<ICorsMetadata>() != null && !ctx.Items.ContainsKey(corsInvoked))
                ThrowCORSMiddlewareMissing(endpoint.DisplayName!);

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

            var epInstance = _epFactory.Create(epDef, ctx);
            epInstance.Definition = epDef;
            epInstance.HttpContext = ctx;

            ResponseCacheExecutor.Execute(ctx, endpoint.Metadata.GetMetadata<ResponseCacheAttribute>());

            //terminate middleware here. we're done executing
            return epInstance.ExecAsync(ctx.RequestAborted);
        }

        return _next(ctx); //this is not a fastendpoint, let next middleware handle it
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