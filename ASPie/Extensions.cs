using ASPie.Security;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Net;
using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        public static WebApplication UseASPie(this WebApplication builder)
        {
            return UseASPie(builder, null);
        }

        public static WebApplication UseASPie(this WebApplication app, IServiceCollection? services) //todo: add ref to Microsoft.AspNetCore.Routing and change SDK to Microsoft.NET.Sdk
        {
            foreach (var endpointType in DiscoveredHandlers())
            {
                var methodInfo = endpointType.GetMethod(
                    "ExecAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (methodInfo == null)
                    throw new ArgumentException($"Unable to find a `HandleAsync` method on: [{endpointType.AssemblyQualifiedName}]");

                var instance = Activator.CreateInstance(endpointType);

                if (instance == null)
                    throw new InvalidOperationException($"Unable to create an instance of: [{endpointType.AssemblyQualifiedName}]");

                var verbs = endpointType.GetFieldValues("verbs", instance);
                if (verbs?.Any() != true)
                    throw new InvalidOperationException($"No HTTP Verbs declared on: [{endpointType.AssemblyQualifiedName}]");

                var routes = endpointType.GetFieldValues("routes", instance);
                if (routes?.Any() != true)
                    throw new InvalidOperationException($"No Routes declared on: [{endpointType.AssemblyQualifiedName}]");

                var enablePermissions = services != null;
                string? permPolicyName = endpointType.AssemblyQualifiedName;
                RegisterPermissionPolicy(services, endpointType, instance, enablePermissions, permPolicyName);

                var deligate = Delegate.CreateDelegate(typeof(RequestDelegate), instance, methodInfo);

                foreach (var route in routes)
                {
                    var b = app.MapMethods(route, verbs, deligate);
                    //todo: here...
                }
            }
            return app;
        }

        private static void RegisterPermissionPolicy(IServiceCollection? services, Type endpointType, object? instance, bool enablePermissions, string? permPolicyName)
        {
            IEnumerable<string>? permissions;
            bool allowAnyPermission = false;

            if (enablePermissions)
            {
                permissions = endpointType.GetFieldValues("permissions", instance);

                if (permissions?.Any() == true)
                {
                    allowAnyPermission = (bool)endpointType.GetFieldValue("allowAnyPermission", instance);

                    if (allowAnyPermission)
                    {
                        services?.AddAuthorizationCore(o => o.AddPolicy(permPolicyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Intersect(permissions)
                                .Any();
                                return hasAny ?? false;
                            });
                        }));
                    }
                    else
                    {
                        services?.AddAuthorizationCore(o => o.AddPolicy(permPolicyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Except(permissions)
                                .Any();
                                return hasAll ?? false;
                            });
                        }));
                    }
                }
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                      !excludes.Any(n => a.FullName.StartsWith(n)))
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsAbstract &&
                       t.GetInterfaces().Contains(typeof(IHandler)));

            if (!handlers.Any())
                throw new InvalidOperationException("Unable to find any handlers that implement `IHandler` interface!");

            return handlers;
        }

        private static IEnumerable<string>? GetFieldValues(this Type type, string fieldName, object instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance) as IEnumerable<string>;
        }

        private static object? GetFieldValue(this Type type, string fieldName, object instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance);
        }
    }
}
