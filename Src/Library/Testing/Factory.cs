using Microsoft.AspNetCore.Http;

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
    /// <param name="httContext">a default http context object</param>
    /// <param name="dependancies">the dependancies of the endpoint if it has injected dependancies</param>
    public static TEndpoint Create<TEndpoint>(DefaultHttpContext httContext, params object?[]? dependancies) where TEndpoint : class, IEndpoint
    {
        var ep = (BaseEndpoint)Activator.CreateInstance(typeof(TEndpoint), dependancies)!;
        ep.Definition = new();
        ep.Configure();
        ep._httpContext = httContext;
        return (ep as TEndpoint)!;
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">an action for configuring the default http context object</param>
    /// <param name="dependancies">the dependancies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(Action<DefaultHttpContext> httpContext, params object?[]? dependancies) where TEndpoint : class, IEndpoint
    {
        var ctx = new DefaultHttpContext();
        httpContext(ctx);
        return Create<TEndpoint>(ctx, dependancies);
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="dependancies">the dependancies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(params object?[]? dependancies) where TEndpoint : class, IEndpoint
    {
        return Create<TEndpoint>(new DefaultHttpContext(), dependancies)!;
    }
}
