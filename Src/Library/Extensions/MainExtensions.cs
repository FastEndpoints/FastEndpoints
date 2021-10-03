using FastEndpoints.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints
{
    public static class MainExtensions
    {
#pragma warning disable CS8618
        private class DiscoveredEndpoint
        {
            public Type EndpointType { get; set; }
            public object EndpointInstance { get; set; }
            public Type? ValidatorType { get; set; }
            public string? SecurityPolicyName { get; set; }
        }
#pragma warning restore CS8618

        private static DiscoveredEndpoint[]? discoveredEndpoints;

        /// <summary>
        /// adds the FastEndpoints services to the ASP.Net middleware pipeline
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddFastEndpoints(this IServiceCollection services)
        {
            DiscoverEndpointsAndValidators();
            services.AddAuthorization(BuildSecurityPoliciesForEndpoints);
            return services;
        }

        private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
        {
            if (discoveredEndpoints is null) return;

            foreach (var ep in discoveredEndpoints)
            {
                var roles = ep.EndpointType.GetFieldValues(nameof(EndpointBase.roles), ep.EndpointInstance);

                var permissions = ep.EndpointType.GetFieldValues(nameof(EndpointBase.permissions), ep.EndpointInstance);
                var allowAnyPermission = (bool?)ep.EndpointType.GetFieldValue(nameof(EndpointBase.allowAnyPermission), ep.EndpointInstance);

                var claims = ep.EndpointType.GetFieldValues(nameof(EndpointBase.claims), ep.EndpointInstance);
                var allowAnyClaim = (bool?)ep.EndpointType.GetFieldValue(nameof(EndpointBase.allowAnyClaim), ep.EndpointInstance);

                if (roles is null && permissions is null && claims is null) continue;

                ep.SecurityPolicyName = $"epPolicy:{ep.EndpointType.FullName}";

                opts.AddPolicy(ep.SecurityPolicyName, b =>
                {
                    if (permissions?.Any() is true)
                    {
                        if (allowAnyPermission is true)
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .FirstOrDefault(c => c.Type == Constants.PermissionsClaimType)?.Value
                                .Split(',')
                                .Intersect(permissions)
                                .Any();
                                return hasAny is true;
                            });
                        }
                        else
                        {
#pragma warning disable CS8602
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !permissions
                                .Except(
                                    x.User.Claims
                                     .FirstOrDefault(c => c.Type == Constants.PermissionsClaimType).Value
                                     .Split(','))
                                .Any();
                                return hasAll is true;
                            });
#pragma warning restore CS8602
                        }
                    }

                    if (claims?.Any() is true)
                    {
                        if (allowAnyClaim is true)
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .Select(c => c.Type)
                                .Intersect(claims)
                                .Any();
                                return hasAny is true;
                            });
                        }
                        else
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !claims
                                .Except(
                                    x.User.Claims
                                     .Select(c => c.Type))
                                .Any();
                                return hasAll is true;
                            });
                        }
                    }

                    if (roles?.Any() is true)
                    {
                        b.RequireRole(roles);
                    }
                });
            }
        }

        /// <summary>
        /// finalizes auto discovery of endpoints and prepares FastEndpoints to start processing requests
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder)
        {
            if (discoveredEndpoints is null) throw new InvalidOperationException($"Please use .{nameof(AddFastEndpoints)}() first!");

            EndpointBase.SerializerOptions = builder.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            BaseEventHandler.ServiceProvider = builder.ServiceProvider;

            foreach (var ep in discoveredEndpoints)
            {
                var epName = ep.EndpointType.FullName;

                var execMethod = ep.EndpointType.GetMethod(nameof(EndpointBase.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);
                if (execMethod is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{epName}]");

                if (ep.EndpointInstance is null) throw new InvalidOperationException($"Unable to create an instance of: [{epName}]");

                var verbs = ep.EndpointType.GetFieldValues(nameof(EndpointBase.verbs), ep.EndpointInstance);
                if (verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");

                var routes = ep.EndpointType.GetFieldValues(nameof(EndpointBase.routes), ep.EndpointInstance);
                if (routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{epName}]");

                var allowAnnonymous = (bool?)ep.EndpointType.GetFieldValue(nameof(EndpointBase.allowAnnonymous), ep.EndpointInstance);

                var userPolicies = ep.EndpointType.GetFieldValues(nameof(EndpointBase.policies), ep.EndpointInstance);
                var policiesToAdd = new List<string>();
                if (userPolicies?.Any() is true) policiesToAdd.AddRange(userPolicies);
                if (ep.SecurityPolicyName is not null) policiesToAdd.Add(ep.SecurityPolicyName);

                var configAction = (Action<DelegateEndpointConventionBuilder>?)ep.EndpointType.GetFieldValue(nameof(EndpointBase.configAction), ep.EndpointInstance);

                var epFactory = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

                EndpointExecutor.CachedServiceBoundProps[ep.EndpointType] = ep.EndpointType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var route in routes.Distinct())
                {
                    var eb = builder.MapMethods(route, verbs, EndpointExecutor.HandleAsync)
                                    .RequireAuthorization(); //secure by default

                    if (policiesToAdd.Count > 0) eb.RequireAuthorization(policiesToAdd.ToArray());
                    if (allowAnnonymous is true) eb.AllowAnonymous();
                    if (configAction is not null) configAction(eb);

                    var validatorInstance = (IValidator?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));

                    EndpointExecutor.CachedEndpointTypes[route] = (epFactory, execMethod, validatorInstance);
                }
            }
            return builder;
        }

        private static void DiscoverEndpointsAndValidators()
        {
            var excludes = new[]
                {
                    "Microsoft.",
                    "System.",
                    "FastEndpoints.",
                    "testhost",
                    "netstandard",
                    "Newtonsoft.",
                    "mscorlib",
                    "NuGet."
                };

#pragma warning disable CS8602
            var discoveredTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                      !a.IsDynamic &&
                      !excludes.Any(n => a.FullName.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsAbstract &&
                      (t.GetInterfaces().Contains(typeof(IEndpoint)) ||
                       t.GetInterfaces().Contains(typeof(IValidator)) ||
                       t.GetInterfaces().Contains(typeof(IEventHandler))));
#pragma warning restore CS8602

            if (!discoveredTypes.Any())
                throw new InvalidOperationException("Unable to find any endpoint declarations!");

            //key: TRequest or BasicEndpoint
            var epDict = new Dictionary<Type, EndpointAndValidatorTypes>();

            foreach (var type in discoveredTypes)
            {
                if (type.IsAssignableTo(typeof(IEventHandler)))
                {
                    ((IEventHandler?)Activator.CreateInstance(type))?.Subscribe();
                    continue;
                }

                Type tRequest = typeof(BasicEndpoint);
                bool hasTRequest = false;

#pragma warning disable CS8600
                if (type.BaseType?.IsGenericType is true)
                {
                    tRequest = type.BaseType?.GetGenericArguments()[0];
                    hasTRequest = true;
                }
#pragma warning restore CS8600
#pragma warning disable CS8604
                if (!epDict.TryGetValue(tRequest, out var val))
                {
                    val = new();
                    epDict.Add(tRequest, val);
                }
#pragma warning restore CS8604
                if (type.IsAssignableTo(typeof(IEndpoint)))
                    val.EndpointType = type;
                else
                    val.ValidatorType = hasTRequest ? type : null;
            }
#pragma warning disable CS8601
            discoveredEndpoints = epDict
                .Select(x => x.Value)
                .Select(x => new DiscoveredEndpoint()
                {
                    EndpointType = x.EndpointType,
                    EndpointInstance = Activator.CreateInstance(x.EndpointType),
                    ValidatorType = x.ValidatorType
                })
                .ToArray();
#pragma warning restore CS8601
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

#pragma warning disable CS8618
        private class EndpointAndValidatorTypes
        {
            public Type EndpointType { get; set; }
            public Type? ValidatorType { get; set; }
        }
#pragma warning restore CS8618
    }
}