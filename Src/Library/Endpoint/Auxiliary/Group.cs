using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// common configuration for a group of endpoints can be specified by implementing this abstract class and calling <see cref="Configure(string, Action{EndpointDefinition})"/> in the constructor.
/// </summary>
public abstract class Group : IServiceResolver
{
    internal Action<EndpointDefinition> Action { get; set; }

    /// <summary>
    /// call this method in the constructor in order to configure the endpoint group.
    /// </summary>
    /// <param name="routePrefix">the route prefix for the group</param>
    /// <param name="ep">the configuration action to be performed on the <see cref="EndpointDefinition"/></param>
    protected virtual void Configure(string routePrefix, Action<EndpointDefinition> ep)
        => Action = RouteModifier(routePrefix) + ep;

    private static Action<EndpointDefinition> RouteModifier(string routePrefix) => e =>
    {
        if (e.Routes?.Length > 0)
        {
            for (var i = 0; i < e.Routes.Length; i++)
            {
                var route = e.Routes[i];
                var slash = !routePrefix.EndsWith("/") && !route.StartsWith("/") ? "/" : "";
                e.Routes[i] = routePrefix + slash + route;
            }
        }
    };

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolve<TService>() where TService : class
        => IServiceResolver.RootServiceProvider.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService)
        => IServiceResolver.RootServiceProvider.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class
        => IServiceResolver.RootServiceProvider.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService)
        => IServiceResolver.RootServiceProvider.GetRequiredService(typeOfService);
    /// <summary>
    /// if you'd like to resolve scoped or transient services from the DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    public IServiceScope CreateScope()
        => IServiceResolver.RootServiceProvider.CreateScope();
}

/// <summary>
/// common configuration for a sub group of endpoints can be specified by implementing this abstract class and calling <see cref="Configure(string, Action{EndpointDefinition})"/> in the constructor.
/// </summary>
/// <typeparam name="TParent"></typeparam>
public abstract class SubGroup<TParent> : Group where TParent : Group, new()
{
    ///<inheritdoc/>
    protected sealed override void Configure(string routePrefix, Action<EndpointDefinition> ep)
    {
        base.Configure(routePrefix, ep);
        Action += new TParent().Action;
    }
}