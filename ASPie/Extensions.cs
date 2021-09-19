using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        public static WebApplication UseASPie(this WebApplication builder)
        {
            return UseASPieWithAuth(builder, null);
        }

        public static WebApplication UseASPieWithAuth(this WebApplication app, IServiceCollection? services) //todo: add ref to Microsoft.AspNetCore.Builder and change SDK to Microsoft.NET.Sdk
        {
            var authEnabled = services != null;

            foreach (var endpointType in DiscoveredHandlers())
            {
                var endpointName = endpointType.AssemblyQualifiedName;

                var methodInfo = endpointType.GetMethod(
                    "ExecAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (methodInfo == null)
                    throw new ArgumentException($"Unable to find a `ExecAsync` method on: [{endpointName}]");

                var instance = Activator.CreateInstance(endpointType);

                if (instance == null)
                    throw new InvalidOperationException($"Unable to create an instance of: [{endpointName}]");

                var verbs = endpointType.GetFieldValues("verbs", instance);
                if (verbs?.Any() != true)
                    throw new InvalidOperationException($"No HTTP Verbs declared on: [{endpointName}]");

                var routes = endpointType.GetFieldValues("routes", instance);
                if (routes?.Any() != true)
                    throw new InvalidOperationException($"No Routes declared on: [{endpointName}]");

                var permissionPolicy = RegisterPermissionPolicy(services, endpointType, instance, authEnabled);

                var deligate = Delegate.CreateDelegate(typeof(RequestDelegate), instance, methodInfo);

                foreach (var route in routes)
                {
                    if (authEnabled)
                    {
                        var builder = app.MapMethods(route, verbs, deligate);

                        var allowAnnonymous = (bool)endpointType.GetFieldValue("allowAnnonymous", instance);

                        if (allowAnnonymous is true)
                        {
                            builder.AllowAnonymous();
                        }
                        else
                        {
                            AddPolicies(endpointType, instance, permissionPolicy, builder);
                            AddRoles(endpointType, instance, builder);
                        }
                    }
                    else
                    {
                        app.MapMethods(route, verbs, deligate);
                    }
                }
            }
            return app;
        }

        private static void AddRoles(Type endpointType, object instance, DelegateEndpointConventionBuilder builder)
        {
            var roles = endpointType.GetFieldValues("roles", instance);
            if (roles?.Any() != true)
            {
                builder.RequireAuthorization(new AuthorizeData
                {
                    Roles = string.Join(',', roles)
                });
            }
        }

        private static void AddPolicies(Type endpointType, object instance, string? permissionPolicy, DelegateEndpointConventionBuilder b)
        {
            var policies = endpointType.GetFieldValues("policies", instance);

            var policiesToAdd = new List<string>();
            if (policies?.Any() == true) policiesToAdd.AddRange(policies);
            if (permissionPolicy != null) policiesToAdd.Add(permissionPolicy);
            b.RequireAuthorization(policiesToAdd.ToArray());
        }

        private static string? RegisterPermissionPolicy(IServiceCollection? services, Type endpointType, object instance, bool authEnabled)
        {
            string? policyName = null;

            if (authEnabled)
            {
                var permissions = endpointType.GetFieldValues("permissions", instance);

                if (permissions?.Any() == true)
                {
                    policyName = "PermPolicy:" + Guid.NewGuid().ToString().Replace("-", "");

                    var allowAnyPermission = (bool?)endpointType.GetFieldValue("allowAnyPermission", instance);

                    if (allowAnyPermission is true)
                    {
                        services?.AddAuthorizationCore(o => o.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Intersect(permissions)
                                .Any();
                                return hasAny is true;
                            });
                        }));
                    }
                    else
                    {
                        services?.AddAuthorizationCore(o => o.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Except(permissions)
                                .Any();
                                return hasAll is true;
                            });
                        }));
                    }
                }
            }
            return policyName;
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
