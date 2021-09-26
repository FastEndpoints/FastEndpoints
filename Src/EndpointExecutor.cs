using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace FastEndpoints
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

            ResolveServices(endpointInstance, ctx);

            return (Task?)execMethod.Invoke(endpointInstance, new object[] { ctx, cancellation })
                ?? Task.CompletedTask;
        }

        private static void ResolveServices(object endpointInstance, HttpContext ctx)
        {
            if (CacheServiceBoundProps.TryGetValue(endpointInstance.GetType(), out var props))
            {
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo? prop = props[i];
                    var serviceInstance = ctx.RequestServices.GetService(prop.PropertyType);
                    prop.SetValue(endpointInstance, serviceInstance);
                }
            }
        }
    }
}
