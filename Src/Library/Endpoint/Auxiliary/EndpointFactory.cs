using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// the default endpoint factory. it resolves the <see cref="EndpointDefinition.EndpointType"/> from the
/// <see cref="HttpContext.RequestServices"/>. both constructor dependencies and property dependencies are injected.
/// </summary>
public class EndpointFactory : IEndpointFactory
{
    public BaseEndpoint Create(EndpointDefinition definition, HttpContext ctx)
    {
        var epInstance = (BaseEndpoint)ctx.RequestServices.GetRequiredService(definition.EndpointType);

        for (var i = 0; i < definition.ServiceBoundEpProps?.Length; i++)
        {
            var prop = definition.ServiceBoundEpProps[i];
            prop.PropSetter(epInstance, ctx.RequestServices.GetRequiredService(prop.PropType));
        }

        return epInstance;
    }
}