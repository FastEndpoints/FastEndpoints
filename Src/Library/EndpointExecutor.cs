using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

[HideFromDocs]
public static class EndpointExecutor
{
    //this is the main handler registered with asp.net for all mapped endpoints/routes. this will be called for each http request.
    public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
    {
        var metaColl = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint?.Metadata;
        var ep = metaColl?.GetMetadata<EndpointMetadata>();

        if (ep is null)
            throw new InvalidOperationException("Critical Error! Endpoint meta data could not be retrieved!");

        var epInstance = (BaseEndpoint)ep.InstanceCreator();

        ResolveServices(epInstance, ctx.RequestServices, ep.ServiceBoundReqDtoProps);

        ResponseCacheExecutor.Execute(ctx, metaColl?.GetMetadata<ResponseCacheAttribute>());

        return epInstance.ExecAsync(ctx, ep.Validator, ep.PreProcessors, ep.PostProcessors, cancellation);
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
}

internal record EndpointMetadata(
    Func<object> InstanceCreator,
    IValidator? Validator,
    ServiceBoundReqDtoProp[]? ServiceBoundReqDtoProps,
    object? PreProcessors,
    object? PostProcessors,
    int Version);

internal record ServiceBoundReqDtoProp(
    Type PropType,
    Action<object, object> PropSetter);