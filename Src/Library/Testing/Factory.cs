using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// a factory for instantiaing endpoints for testing purposes
/// </summary>
public static class Factory
{
    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">a default http context object</param>
    /// <param name="dependencies">the dependencies of the endpoint if it has injected dependencies</param>
    public static TEndpoint Create<TEndpoint>(DefaultHttpContext httpContext, params object?[]? dependencies) where TEndpoint : class, IEndpoint
    {
        if (Config.ServiceResolver is null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(Event<>));
            Config.ServiceResolver = new ServiceResolver(services.BuildServiceProvider());
        }

        var tEndpoint = typeof(TEndpoint);
        var ep = (BaseEndpoint)Activator.CreateInstance(tEndpoint, dependencies)!;
        ep.Definition = new()
        {
            EndpointType = tEndpoint,
            ReqDtoType = tEndpoint.GetGenericArgumentsOfType(Types.EndpointOf2)?[0] ?? Types.EmptyRequest,
        };
        ep.Definition.Initialize(ep, httpContext);
        return (ep as TEndpoint)!;
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">an action for configuring the default http context object</param>
    /// <param name="dependencies">the dependencies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(Action<DefaultHttpContext> httpContext, params object?[]? dependencies) where TEndpoint : class, IEndpoint
    {
        var ctx = new DefaultHttpContext();
        httpContext(ctx);
        return Create<TEndpoint>(ctx, dependencies);
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="dependencies">the dependencies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(params object?[]? dependencies) where TEndpoint : class, IEndpoint
    {
        return Create<TEndpoint>(new DefaultHttpContext(), dependencies)!;
    }
}
