using System.Linq.Expressions;
using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        public static DelegateEndpointConventionBuilder UseASPie(this IEndpointRouteBuilder b) //todo: add ref to Microsoft.AspNetCore.Routing and change SDK to Microsoft.NET.Sdk
        {
            DelegateEndpointConventionBuilder? builder = null;

            foreach (var tHandler in DiscoveredHandlers())
            {
                var methodInfo = tHandler.GetMethod(
                    "HandleAsync",
                    BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);

                if (methodInfo == null)
                    throw new ArgumentException($"Unable to find a `HandleAsync` method on: [{tHandler.AssemblyQualifiedName}]");

                var instance = Activator.CreateInstance(tHandler);

                if (instance == null)
                    throw new InvalidOperationException($"Unable to create an instance of: [{tHandler.AssemblyQualifiedName}]");

                var verbs = (tHandler.GetField("verbs")?.GetValue(instance) as IEnumerable<Http>)?.Cast<string>();

                if (verbs?.Any() != true)
                    throw new InvalidOperationException($"No HTTP Verbs declared on: [{tHandler.AssemblyQualifiedName}]");

                var routes = tHandler.GetField("routes")?.GetValue(instance) as IEnumerable<string>;

                if (routes?.Any() != true)
                    throw new InvalidOperationException($"No Routes declared on: [{tHandler.AssemblyQualifiedName}]");

                var del = methodInfo.CreateDelegate(instance);

                foreach (var route in routes)
                    builder = b.MapMethods(route, verbs, del);
            }

            if (builder is null)
                throw new InvalidOperationException("Automatic handler mapping was not successful!");

            return builder;
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
                .Where(t => t.GetInterfaces().Contains(typeof(IHandler)));

            if (!handlers.Any())
                throw new InvalidOperationException("Unable to find any handlers that implement `IHandler` interface!");

            return handlers;
        }

        private static Delegate CreateDelegate(this MethodInfo methodInfo, object target) //credit: https://stackoverflow.com/a/40579063/4368485
        {
            Func<Type[], Type> getType;
            var isAction = methodInfo.ReturnType.Equals(typeof(void));
            var types = methodInfo.GetParameters().Select(p => p.ParameterType);

            if (isAction)
            {
                getType = Expression.GetActionType;
            }
            else
            {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { methodInfo.ReturnType });
            }

            if (methodInfo.IsStatic)
            {
                return Delegate.CreateDelegate(getType(types.ToArray()), methodInfo);
            }

            return Delegate.CreateDelegate(getType(types.ToArray()), target, methodInfo.Name);
        }

    }
}
