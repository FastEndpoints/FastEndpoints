using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// a factory for instantiaing endpoints for testing purposes
/// </summary>
public static class Factory
{
    private static readonly IEndpointFactory epFactory = new EndpointFactory();

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">a default http context object</param>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected dependencies</param>
    public static TEndpoint Create<TEndpoint>(DefaultHttpContext httpContext, params object?[] ctorDependencies) where TEndpoint : class, IEndpoint
    {
        if (Config.ServiceResolver is null) //only ever set it once
        {
            var services = new ServiceCollection();
            services.AddServicesForUnitTesting();
            var svcProvider = services.BuildServiceProvider();
            Config.ServiceResolver = new ServiceResolver(svcProvider);
        }

        BaseEndpoint ep;
        var tEndpoint = typeof(TEndpoint);
        var epDef = new EndpointDefinition(
            tEndpoint,
            tEndpoint.GetGenericArgumentsOfType(Types.EndpointOf2)?[0] ?? Types.EmptyRequest);

        if (ctorDependencies.Length > 0)
            ep = (BaseEndpoint)Activator.CreateInstance(tEndpoint, ctorDependencies)!; //ctor injection only
        else
            ep = epFactory.Create(epDef, httpContext); //ctor & property injection

        epDef.Initialize(ep, httpContext);
        return (ep as TEndpoint)!;
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">an action for configuring the default http context object</param>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(Action<DefaultHttpContext> httpContext, params object?[] ctorDependencies) where TEndpoint : class, IEndpoint
    {
        var ctx = new DefaultHttpContext();
        httpContext(ctx);
        return Create<TEndpoint>(ctx, ctorDependencies);
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected dependencies</param>
    public static TEndpoint Create<TEndpoint>(params object?[] ctorDependencies) where TEndpoint : class, IEndpoint
    {
        return Create<TEndpoint>(new DefaultHttpContext(), ctorDependencies)!;
    }

    /// <summary>
    /// adds the minimum required set of services for unit testing FE endpoints
    /// </summary>
    public static IServiceCollection AddServicesForUnitTesting(this IServiceCollection services)
    {
        return services
             .AddSingleton<ILoggerFactory, LoggerFactory>()
             .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
             .AddSingleton(typeof(Event<>));
    }

    /// <summary>
    /// register fake/mock/test services for the http context. typically only used with unit tests with the <c>Factory.Create()</c>" method/>
    /// </summary>
    /// <param name="s">an action for adding services to the <see cref="IServiceCollection"/></param>
    /// <exception cref="InvalidOperationException">thrown if the <see cref="HttpContext.RequestServices"/> is not empty</exception>
    public static void AddTestServices(this HttpContext ctx, Action<IServiceCollection> s)
    {
        if (ctx.RequestServices is not null) throw new InvalidOperationException("You cannot add services to this http context!");

        var collection = new ServiceCollection();
        collection.AddServicesForUnitTesting();
        s(collection);
        ctx.RequestServices = collection.BuildServiceProvider();
    }
}