using System.Reflection;

namespace EZEndpoints
{
    public static class EndpointExecutor
    {
        internal static Dictionary<string, (Func<object> endpointFactory, MethodInfo execMethod)> CacheEndpointTypes { get; } = new();
        internal static Dictionary<Type, PropertyInfo[]> CacheServiceBoundProps { get; } = new();

        //note: this handler is called by .net for each http request
        public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
        {
            var route = ((RouteEndpoint?)ctx.GetEndpoint())?.RoutePattern.RawText;
            if (route is null) throw new InvalidOperationException("Unable to instantiate endpoint!!!");

            var (endpointFactory, execMethod) = CacheEndpointTypes[route];

            var endpointInstance = endpointFactory();

            ResolveServices(endpointInstance);

            return (Task?)execMethod.Invoke(endpointInstance, new object[] { ctx, cancellation })
                ?? Task.CompletedTask;
        }

        private static void ResolveServices(object endpointInstance)
        {
            if (CacheServiceBoundProps.TryGetValue(endpointInstance.GetType(), out var props))
            {
                foreach (var prop in props)
                {
                    var serviceInstance = Endpoint.serviceProvider.GetService(prop.PropertyType);
                    prop.SetValue(endpointInstance, serviceInstance);
                }
            }
        }
    }
}
