namespace FastEndpoints;

public abstract class EndpointGroup
{
    internal Action<EndpointDefinition> Action { get; set; }

    protected void Configure(string routePrefix, Action<EndpointDefinition> ep)
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
}