using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace FastEndpoints
{
    public static class EndpointExecutor
    {
        //key: route url for the endpoint
        internal static Dictionary<string, (Func<object> endpointFactory, MethodInfo execAsyncMethod, IValidator? validator)> CachedEndpointTypes { get; } = new();

        //key: type of endpoint
        internal static Dictionary<Type, PropertyInfo[]> CachedServiceBoundProps { get; } = new();

        //note: this handler is called by .net for each http request
        public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
        {
            var route = ((RouteEndpoint?)ctx.GetEndpoint())?.RoutePattern.RawText;
            if (route is null) throw new InvalidOperationException("Unable to instantiate endpoint!!!");

            var (endpointFactory, execAsyncMethod, validator) = CachedEndpointTypes[route];

            var endpointInstance = endpointFactory();

            ResolveServices(endpointInstance, ctx);

#pragma warning disable CS8601
            return (Task?)execAsyncMethod.Invoke(endpointInstance, new object[] { ctx, validator, cancellation })
                ?? Task.CompletedTask;
#pragma warning restore CS8601
        }

        private static void ResolveServices(object endpointInstance, HttpContext ctx)
        {
            if (CachedServiceBoundProps.TryGetValue(endpointInstance.GetType(), out var props))
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
