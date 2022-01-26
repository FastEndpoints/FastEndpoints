using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FastEndpoints;

internal class ExecutorMiddleware
{
    private const string AuthorizationMiddlewareInvoked = "__AuthorizationMiddlewareWithEndpointInvoked";
    private const string CorsMiddlewareInvoked = "__CorsMiddlewareWithEndpointInvoked";
    private readonly RequestDelegate _next;
    private readonly RouteOptions _routeOptions;

    public ExecutorMiddleware(RequestDelegate next, IOptions<RouteOptions> routeOptions)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _routeOptions = routeOptions?.Value ?? throw new ArgumentNullException(nameof(routeOptions));
    }

    public Task Invoke(HttpContext ctx)
    {
        var endpoint = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint;

        if (endpoint is null) return _next(ctx);

        var epMetaData = endpoint.Metadata.GetMetadata<EndpointMetadata>();

        if (epMetaData is not null)
        {
            if (endpoint.Metadata.GetMetadata<IAuthorizeData>() != null && !ctx.Items.ContainsKey(AuthorizationMiddlewareInvoked))
                ThrowMissingAuthMiddlewareException(endpoint.DisplayName!);

            if (endpoint.Metadata.GetMetadata<ICorsMetadata>() != null && !ctx.Items.ContainsKey(CorsMiddlewareInvoked))
                ThrowMissingCorsMiddlewareException(endpoint.DisplayName!);

            var epInstance = (BaseEndpoint)epMetaData.InstanceCreator();

            ResolveServices(epInstance, ctx.RequestServices, epMetaData.ServiceBoundReqDtoProps);

            ResponseCacheExecutor.Execute(ctx, endpoint.Metadata.GetMetadata<ResponseCacheAttribute>());

            return epInstance.ExecAsync(
                ctx,
                epMetaData.Validator,
                epMetaData.PreProcessors,
                epMetaData.PostProcessors,
                ctx.RequestAborted); //terminate middleware here we're done executing
        }

        return _next(ctx); //this is not a fastendpoint, let next middleware handle it
    }

    private static void ResolveServices(object epInstance, IServiceProvider services, ServiceBoundReqDtoProp[]? props)
    {
        if (props is null) return;

        for (int i = 0; i < props.Length; i++)
        {
            ServiceBoundReqDtoProp p = props[i];
            p.PropSetter(epInstance, services.GetRequiredService(p.PropType));
        }
    }

    private static void ThrowMissingAuthMiddlewareException(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains authorization metadata, " +
            "but a middleware was not found that supports authorization." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseAuthorization() in the application startup code. If there are calls to app.UseRouting() and app.UseEndpoints(...), the call to app.UseAuthorization() must go between them.");
    }

    private static void ThrowMissingCorsMiddlewareException(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains CORS metadata, " +
            "but a middleware was not found that supports CORS." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseCors() in the application startup code. If there are calls to app.UseRouting() and app.UseEndpoints(...), the call to app.UseCors() must go between them.");
    }
}
