namespace FastEndpoints;

/// <summary>
/// common configuration for a group of endpoints can be specified by implementing this abstract class and calling
/// <see cref="Configure(string, Action{EndpointDefinition})" /> in the constructor.
/// </summary>
public abstract class Group : ServiceResolverClient
{
    internal Action<EndpointDefinition> Action { get; set; } = null!;

    /// <summary>
    /// call this method in the constructor in order to configure the endpoint group.
    /// </summary>
    /// <param name="routePrefix">the route prefix for the group</param>
    /// <param name="ep">the configuration action to be performed on the <see cref="EndpointDefinition" /></param>
    protected void Configure(string routePrefix, Action<EndpointDefinition> ep)
    {
        Action = RouteModifier(routePrefix) + ep;
        var inherited = InheritedAction();

        if (inherited is not null)
            Action += inherited;
    }

    /// <summary>
    /// extra configuration from a parent group. <see cref="SubGroup{TParent}" /> uses this so the parent action runs after this group's own.
    /// </summary>
    internal virtual Action<EndpointDefinition>? InheritedAction()
        => null;

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
}

/// <summary>
/// common configuration for a sub group of endpoints can be specified by implementing this abstract class and calling
/// <see cref="Group.Configure(string, Action{EndpointDefinition})" /> in the constructor.
/// </summary>
/// <typeparam name="TParent"></typeparam>
public abstract class SubGroup<TParent> : Group where TParent : Group, new()
{
    /// <inheritdoc />
    internal sealed override Action<EndpointDefinition>? InheritedAction()
        => new TParent().Action;
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