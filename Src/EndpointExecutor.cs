using System.Reflection;

namespace EZEndpoints
{
    public static class EndpointExecutor
    {
        internal static Dictionary<string, (Func<object> creator, MethodInfo method)> EndpointTypeCache { get; } = new();
        internal static Dictionary<Type, PropertyInfo[]> PropsToBindServicesCache { get; } = new();

        public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
        {
            var route = ((RouteEndpoint?)ctx.GetEndpoint())?.RoutePattern.RawText;
            if (route is null) throw new InvalidOperationException("Unable to instantiate endpoint!!!");

            var (creator, method) = EndpointTypeCache[route];

            var instance = creator();

            ResolveServices(instance);

            return (Task?)method.Invoke(instance, new object[] { ctx, cancellation })
                ?? Task.CompletedTask;
        }

        private static void ResolveServices(object instance)
        {
            if (PropsToBindServicesCache.TryGetValue(instance.GetType(), out var props))
            {
                foreach (var prop in props)
                {
                    var service = Endpoint.serviceProvider.GetService(prop.PropertyType);
                    prop.SetValue(instance, service);
                }
            }
        }
    }
}
