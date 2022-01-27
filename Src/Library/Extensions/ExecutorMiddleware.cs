using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal class ExecutorMiddleware
{
    private const string AuthorizationMiddlewareInvoked = "__AuthorizationMiddlewareWithEndpointInvoked";
    private const string CorsMiddlewareInvoked = "__CorsMiddlewareWithEndpointInvoked";
    private readonly RequestDelegate _next;

    public ExecutorMiddleware(RequestDelegate next)
        => _next = next ?? throw new ArgumentNullException(nameof(next));

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
                ctx.RequestAborted); //terminate middleware here. we're done executing
        }

        return _next(ctx); //this is not a fastendpoint, let next middleware handle it
    }

    private static void ResolveServices(object epInstance, IServiceProvider services, ServiceBoundReqDtoProp[]? props)
    {
        if (props is null) return;

        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            p.PropSetter(epInstance, services.GetRequiredService(p.PropType));
        }
    }

    private static void ThrowMissingAuthMiddlewareException(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains authorization metadata, " +
            "but a middleware was not found that supports authorization." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseAuthorization() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints() the call to app.UseAuthorization() must go between them.");
    }

    private static void ThrowMissingCorsMiddlewareException(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains CORS metadata, " +
            "but a middleware was not found that supports CORS." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseCors() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints(), the call to app.UseCors() must go between them.");
    }
}
