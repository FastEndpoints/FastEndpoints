using ApiExpress.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace ApiExpress
{
    public static class MainExtensions
    {
        private static (Type endpointType, object? endpointInstance, string? endpointName)[]? endpoints;

        public static IServiceCollection AddApiExpress(this IServiceCollection services) //add ref to Microsoft.AspNetCore.Builder and change SDK to Microsoft.NET.Sdk
        {
            endpoints = DiscoverEndpointTypes()
                .Select(t => (t, Activator.CreateInstance(t), t.AssemblyQualifiedName)).ToArray();

            services.AddAuthorization(BuildPermissionPolicies);

            return services;
        }

        public static IEndpointRouteBuilder UseApiExpress(this IEndpointRouteBuilder builder)
        {
            Endpoint.serviceProvider = builder.ServiceProvider;

            if (endpoints is null) throw new InvalidOperationException("Please use .UseEZEndpoints() first!");

            foreach (var (epType, epInstance, epName) in endpoints)
            {
                var execMethod = epType.GetMethod(nameof(Endpoint.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);
                if (execMethod is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{epName}]");

                if (epInstance is null) throw new InvalidOperationException($"Unable to create an instance of: [{epName}]");

                var verbs = epType.GetFieldValues(nameof(Endpoint.verbs), epInstance);
                if (verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");

                var routes = epType.GetFieldValues(nameof(Endpoint.routes), epInstance);
                if (routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{epName}]");

                var allowAnnonymous = (bool?)epType.GetFieldValue(nameof(Endpoint.allowAnnonymous), epInstance);
                var acceptFiles = (bool?)epType.GetFieldValue(nameof(Endpoint.acceptFiles), epInstance);

                string? permissionPolicyName = null;
                var permissions = epType.GetFieldValues(nameof(Endpoint.permissions), epInstance);
                if (permissions?.Any() is true) permissionPolicyName = $"{ClaimTypes.Permissions}:{epName}";

                var userPolicies = epType.GetFieldValues(nameof(Endpoint.policies), epInstance);
                var policiesToAdd = new List<string>();
                if (userPolicies?.Any() is true) policiesToAdd.AddRange(userPolicies);
                if (permissionPolicyName is not null) policiesToAdd.Add(permissionPolicyName);

                var userRoles = epType.GetFieldValues(nameof(Endpoint.roles), epInstance);
                var rolesToAdd = userRoles?.Any() is true ? string.Join(',', userRoles) : null;

                var epFactory = Expression.Lambda<Func<object>>(Expression.New(epType)).Compile();

                EndpointExecutor.CacheServiceBoundProps[epType] = epType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var route in routes.Distinct())
                {
                    var eb = builder.MapMethods(route, verbs, EndpointExecutor.HandleAsync)
                                    .RequireAuthorization(); //secure by default

                    if (acceptFiles is true) eb.Accepts<IFormFile>("multipart/form-data");
                    if (allowAnnonymous is true) eb.AllowAnonymous();
                    if (policiesToAdd.Count > 0) eb.RequireAuthorization(policiesToAdd.ToArray());
                    if (rolesToAdd is not null) eb.RequireAuthorization(new AuthorizeData { Roles = rolesToAdd });

                    EndpointExecutor.CacheEndpointTypes[route] = (epFactory, execMethod);
                }
            }
            endpoints = null;
            return builder;
        }

        private static void BuildPermissionPolicies(AuthorizationOptions options)
        {
            if (endpoints is null) return;

            foreach (var (epType, epInstance, epName) in endpoints)
            {
                var permissions = epType.GetFieldValues(nameof(Endpoint.permissions), epInstance);

                if (permissions?.Any() is true)
                {
                    var policyName = $"{ClaimTypes.Permissions}:{epName}";
                    var allowAnyPermission = (bool?)epType.GetFieldValue(nameof(Endpoint.allowAnyPermission), epInstance);

                    if (allowAnyPermission is true)
                    {
                        options.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .FirstOrDefault(c => c.Type == ClaimTypes.Permissions)?
                                .Value
                                .Split(',')
                                .Intersect(permissions)
                                .Any();
                                return hasAny is true;
                            });
                        });
                    }
                    else
                    {
                        options.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !x.User.Claims
                                .FirstOrDefault(c => c.Type == ClaimTypes.Permissions)?
                                .Value
                                .Split(',')
                                .Except(permissions)
                                .Any();
                                return hasAll is true;
                            });
                        });
                    }
                }
            }
        }

        private static IEnumerable<Type> DiscoverEndpointTypes()
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

            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                      !a.IsDynamic &&
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                      !excludes.Any(n => a.FullName.StartsWith(n)))
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsAbstract &&
                       t.GetInterfaces().Contains(typeof(IEndpoint)));

            if (!types.Any())
                throw new InvalidOperationException("Unable to find any endpoint declarations!");

            return types;
        }

        private static IEnumerable<string>? GetFieldValues(this Type type, string fieldName, object? instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance) as IEnumerable<string>;
        }

        private static object? GetFieldValue(this Type type, string fieldName, object? instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance);
        }
    }
}