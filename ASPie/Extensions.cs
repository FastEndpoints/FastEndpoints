using System.Linq.Expressions;
using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        public static void UseASPie(this IEndpointRouteBuilder b) //todo: add ref to Microsoft.AspNetCore.Routing and change SDK to Microsoft.NET.Sdk
        {
            foreach (var handlerType in DiscoveredHandlers())
            {
                var methodInfo = handlerType.GetMethod(
                    "HandleAsync",
                    BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);

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

                var del = methodInfo.CreateDelegate(instance);

                foreach (var route in routes)
                    b.MapMethods(route, verbs, del);
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
