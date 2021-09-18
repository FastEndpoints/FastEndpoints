using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        public static void UseASPie(this IEndpointRouteBuilder builder) //todo: add ref to Microsoft.AspNetCore.Routing and change SDK to Microsoft.NET.Sdk
        {
            foreach (var handlerType in DiscoveredHandlers())
            {
                var methodInfo = handlerType.GetMethod(
                    "ExecAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (methodInfo == null)
                    throw new ArgumentException($"Unable to find a `HandleAsync` method on: [{handlerType.AssemblyQualifiedName}]");

                var instance = Activator.CreateInstance(handlerType);

                if (instance == null)
                    throw new InvalidOperationException($"Unable to create an instance of: [{handlerType.AssemblyQualifiedName}]");

                var verbs = (handlerType.BaseType?.GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as IEnumerable<Http>)?
                    .Select(v => v.ToString());

                if (verbs?.Any() != true)
                    throw new InvalidOperationException($"No HTTP Verbs declared on: [{handlerType.AssemblyQualifiedName}]");

                var routes = handlerType.BaseType?.GetField("routes", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as IEnumerable<string>;

                if (routes?.Any() != true)
                    throw new InvalidOperationException($"No Routes declared on: [{handlerType.AssemblyQualifiedName}]");

                var reqDeligate = Delegate.CreateDelegate(typeof(RequestDelegate), instance, methodInfo);

                foreach (var route in routes)
                    builder.MapMethods(route, verbs, reqDeligate);
            }
        }

        private static IEnumerable<Type> DiscoveredHandlers()
        {
            var excludes = new[]
                {
                    "Microsoft.",
                    "System.",
                    "MongoDB.",
                    "testhost",
                    "netstandard",
                    "Newtonsoft.",
                    "mscorlib",
                    "NuGet."
                };

            var handlers = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                      !a.IsDynamic &&
                      !excludes.Any(n => a.FullName.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsAbstract &&
                       t.GetInterfaces().Contains(typeof(IHandler)));

            if (!handlers.Any())
                throw new InvalidOperationException("Unable to find any handlers that implement `IHandler` interface!");

            return handlers;
        }
    }
}
