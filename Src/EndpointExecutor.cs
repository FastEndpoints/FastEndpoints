using System.Reflection;

namespace EZEndpoints
{
    public static class EndpointExecutor
    {
        internal static Dictionary<string, (Func<object> creator, MethodInfo method)> EndpointTypeCache { get; } = new();

        public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
        {
            var route = ((RouteEndpoint?)ctx.GetEndpoint())?.RoutePattern.RawText;
            if (route is null) throw new InvalidOperationException("Unable to instantiate endpoint!!!");

            var (creator, method) = EndpointTypeCache[route];

            return (Task?)method.Invoke(creator(), new object[] { ctx, cancellation })
                ?? Task.CompletedTask;
        }
    }
}
