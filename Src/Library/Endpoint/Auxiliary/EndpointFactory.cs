using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// the default endpoint factory.
/// it creates an instance of the endpoint using the cached delegate <see cref="EndpointDefinition.EpInstanceCreator"/> by supplying it with the <see cref="IServiceProvider"/> from <see cref="HttpContext.RequestServices"/>. 
/// both constructor dependencies and property dependencies are injected.
/// </summary>
public class EndpointFactory : IEndpointFactory
{
    /// <summary>
    /// this method is called per each request.
    /// </summary>
    /// <param name="definition">the endpoint definition</param>
    /// <param name="ctx">the http context for the current request</param>
    public BaseEndpoint Create(EndpointDefinition definition, HttpContext ctx)
    {
        var epInstance = (BaseEndpoint)definition.EpInstanceCreator(ctx.RequestServices, null);

        for (var i = 0; i < definition.ServiceBoundEpProps?.Length; i++)
        {
            var prop = definition.ServiceBoundEpProps[i];
            prop.PropSetter(epInstance, ctx.RequestServices.GetRequiredService(prop.PropType));
        }

        return epInstance;
    }
}