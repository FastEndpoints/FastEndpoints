using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// interface for the creation of endpoints.
/// </summary>
public interface IEndpointFactory
{
    BaseEndpoint Create(Endpoint endpoint, HttpContext ctx);
}

/// <summary>
/// The default endpoint factory. It resolves the <see cref="EndpointDefinition.EndpointType"/> from the
/// <see cref="HttpContext.RequestServices"/>. Both constructor dependencies and property dependencies are injected.
/// </summary>
public class DefaultEndpointFactory : IEndpointFactory
{
    public BaseEndpoint Create(Endpoint endpoint, HttpContext ctx)
    {
        var epDef = endpoint.Metadata.GetMetadata<EndpointDefinition>();
        var epInstance = (BaseEndpoint)ctx.RequestServices.GetRequiredService(epDef!.EndpointType);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;
        ResolveServices(epInstance, ctx.RequestServices, epDef.ServiceBoundEpProps);
        return epInstance;
    }

    private static void ResolveServices(object epInstance, IServiceProvider services, ServiceBoundEpProp[]? props)
    {
        if (props is null) return;

        for (var i = 0; i < props.Length; i++)
        {
            var p = props[i];
            p.PropSetter(epInstance, services.GetRequiredService(p.PropType));
        }
    }
}