using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// the default endpoint factory.
/// it creates an instance of the endpoint and injects both constructor and property dependencies.
/// </summary>
public sealed class EndpointFactory : IEndpointFactory
{
    /// <summary>
    /// this method is called per each request.
    /// </summary>
    /// <param name="definition">the endpoint definition</param>
    /// <param name="ctx">the http context for the current request</param>
    public BaseEndpoint Create(EndpointDefinition definition, HttpContext ctx)
    {
        //note: if the default factory is being called, that means it's ok to use HttpContext.RequestServices below since the default MS DI is being used

        var epInstance = (BaseEndpoint)Config.ServiceResolver.CreateInstance(definition.EndpointType, ctx.RequestServices);

        var isAppStartup = ctx.Connection.Id == null;

        for (var i = 0; i < definition.ServiceBoundEpProps?.Length; i++)
        {
            var prop = definition.ServiceBoundEpProps[i];
            prop.PropSetter ??= definition.EndpointType.SetterForProp(prop.PropName);
            prop.PropSetter(
                epInstance,
                isAppStartup
                 ? ctx.RequestServices.GetRequiredService(prop.PropType)
                 : ctx.Resolve(prop.PropType));
        }

        return epInstance;
    }
}