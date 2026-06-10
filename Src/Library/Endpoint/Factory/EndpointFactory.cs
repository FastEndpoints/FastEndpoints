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
    /// <param name="sp">the service provider for the current request</param>
    public BaseEndpoint Create(EndpointDefinition definition, IServiceProvider sp)
    {
        //note: if the default factory is being called, that means it's ok to use HttpContext.RequestServices below since the default MS DI is being used

        var epInstance = (BaseEndpoint)ServiceResolver.Instance.CreateInstance(definition.EndpointType, sp);

        for (var i = 0; i < definition.ServiceBoundEpProps.Length; i++)
        {
            var p = definition.ServiceBoundEpProps[i];
            p.PropSetter ??= definition.EndpointType.SetterForProp(p.PropertyInfo);
            p.PropSetter(epInstance, ResolveService(sp, p));
        }

        return epInstance;
    }

    static object ResolveService(IServiceProvider sp, ServiceBoundEpProp p)
        => p.ServiceKey switch
        {
            not null => sp.GetRequiredKeyedService(p.PropertyInfo.PropertyType, p.ServiceKey),
            _ => sp.GetRequiredService(p.PropertyInfo.PropertyType)
        };
}