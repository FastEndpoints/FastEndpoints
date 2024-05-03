using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// common configuration for a group of endpoints can be specified by implementing this abstract class and calling
/// <see cref="Configure(string, Action{EndpointDefinition})" /> in the constructor.
/// </summary>
public abstract class Group : IServiceResolverBase
{
    internal Action<EndpointDefinition> Action { get; set; } = null!;

    /// <summary>
    /// call this method in the constructor in order to configure the endpoint group.
    /// </summary>
    /// <param name="routePrefix">the route prefix for the group</param>
    /// <param name="ep">the configuration action to be performed on the <see cref="EndpointDefinition" /></param>
    protected virtual void Configure(string routePrefix, Action<EndpointDefinition> ep)
        => Action = RouteModifier(routePrefix) + ep;

    static Action<EndpointDefinition> RouteModifier(string routePrefix)
        => e =>
           {
               if (!(e.Routes.Length > 0))
                   return;

               for (var i = 0; i < e.Routes.Length; i++)
               {
                   var route = e.Routes[i];
                   var slash = !routePrefix.EndsWith('/') && !route.StartsWith('/') ? "/" : "";
                   e.Routes[i] = routePrefix + slash + route;
               }
           };

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => Cfg.ServiceResolver.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => Cfg.ServiceResolver.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => Cfg.ServiceResolver.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => Cfg.ServiceResolver.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => Cfg.ServiceResolver.CreateScope();

#if NET8_0_OR_GREATER
    /// <inheritdoc />
    public TService? TryResolve<TService>(string keyName) where TService : class
        => Cfg.ServiceResolver.TryResolve<TService>(keyName);

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService, string keyName)
        => Cfg.ServiceResolver.TryResolve(typeOfService, keyName);

    /// <inheritdoc />
    public TService Resolve<TService>(string keyName) where TService : class
        => Cfg.ServiceResolver.Resolve<TService>(keyName);

    /// <inheritdoc />
    public object Resolve(Type typeOfService, string keyName)
        => Cfg.ServiceResolver.Resolve(typeOfService, keyName);
#endif
}

/// <summary>
/// common configuration for a sub group of endpoints can be specified by implementing this abstract class and calling
/// <see cref="Configure(string, Action{EndpointDefinition})" /> in the constructor.
/// </summary>
/// <typeparam name="TParent"></typeparam>
public abstract class SubGroup<TParent> : Group where TParent : Group, new()
{
    /// <inheritdoc />
    protected sealed override void Configure(string routePrefix, Action<EndpointDefinition> ep)
    {
        base.Configure(routePrefix, ep);
        Action += new TParent().Action;
    }
}

interface IGroupAttribute
{
    void InitGroup(EndpointDefinition def);
}

/// <summary>
/// generic attribute for designating a group that an endpoint belongs. only effective when attribute based endpoint configuration is being used.
/// </summary>
/// <typeparam name="TEndpointGroup">the type of the group class to use for this endpoint</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GroupAttribute<TEndpointGroup> : Attribute, IGroupAttribute where TEndpointGroup : Group, new()
{
#pragma warning disable CA1822
    void IGroupAttribute.InitGroup(EndpointDefinition def)
        => new TEndpointGroup().Action(def);
#pragma warning restore CA1822
}