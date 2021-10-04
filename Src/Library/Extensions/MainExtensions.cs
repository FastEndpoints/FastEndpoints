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
        private static bool okToClearDiscoveredEndpoints;

        /// <summary>
        /// adds the FastEndpoints services to the ASP.Net middleware pipeline
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddFastEndpoints(this IServiceCollection services)
        {
            Discover_Endpoints_Validators_EventHandlers();
            services.AddAuthorization(BuildSecurityPoliciesForEndpoints);
            return services;
        }

        private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
        {
            if (discoveredEndpoints is null) return;

            foreach (var ep in discoveredEndpoints)
            {
                var roles = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.roles), ep.EndpointInstance);

                var permissions = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.permissions), ep.EndpointInstance);
                var allowAnyPermission = (bool?)ep.EndpointType.GetFieldValue(nameof(BaseEndpoint.allowAnyPermission), ep.EndpointInstance);

                var claims = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.claims), ep.EndpointInstance);
                var allowAnyClaim = (bool?)ep.EndpointType.GetFieldValue(nameof(BaseEndpoint.allowAnyClaim), ep.EndpointInstance);

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
            okToClearDiscoveredEndpoints = true;
        }

        /// <summary>
        /// finalizes auto discovery of endpoints and prepares FastEndpoints to start processing requests
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder)
        {
            if (discoveredEndpoints is null) throw new InvalidOperationException($"Please use .{nameof(AddFastEndpoints)}() first!");

            BaseEndpoint.SerializerOptions = builder.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            BaseEventHandler.ServiceProvider = builder.ServiceProvider;

            foreach (var ep in discoveredEndpoints)
            {
                var epName = ep.EndpointType.FullName;

                var execMethod = ep.EndpointType.GetMethod(nameof(BaseEndpoint.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);
                if (execMethod is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{epName}]");

                if (ep.EndpointInstance is null) throw new InvalidOperationException($"Unable to create an instance of: [{epName}]");

                var verbs = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.verbs), ep.EndpointInstance);
                if (verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");

                var routes = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.routes), ep.EndpointInstance);
                if (routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{epName}]");

                var allowAnnonymous = (bool?)ep.EndpointType.GetFieldValue(nameof(BaseEndpoint.allowAnnonymous), ep.EndpointInstance);

                var userPolicies = ep.EndpointType.GetFieldValues(nameof(BaseEndpoint.policies), ep.EndpointInstance);
                var policiesToAdd = new List<string>();
                if (userPolicies?.Any() is true) policiesToAdd.AddRange(userPolicies);
                if (ep.SecurityPolicyName is not null) policiesToAdd.Add(ep.SecurityPolicyName);

                var intConfigAction = (Action<DelegateEndpointConventionBuilder>?)ep.EndpointType.GetFieldValue(nameof(BaseEndpoint.internalConfigAction), ep.EndpointInstance);
                var usrConfigAction = (Action<DelegateEndpointConventionBuilder>?)ep.EndpointType.GetFieldValue(nameof(BaseEndpoint.userConfigAction), ep.EndpointInstance);

                var epFactory = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

                EndpointExecutor.CachedServiceBoundProps[ep.EndpointType] = ep.EndpointType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var route in routes.Distinct())
                {
                    var eb = builder.MapMethods(route, verbs, EndpointExecutor.HandleAsync)
                                    .RequireAuthorization(); //secure by default

                    if (policiesToAdd.Count > 0) eb.RequireAuthorization(policiesToAdd.ToArray());
                    if (allowAnnonymous is true) eb.AllowAnonymous();
                    if (intConfigAction is not null) intConfigAction(eb);
                    if (usrConfigAction is not null) usrConfigAction(eb);

                    var validatorInstance = (IValidator?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));

                    EndpointExecutor.CachedEndpointTypes[route] = (epFactory, execMethod, validatorInstance);
                }
            }

            Task.Run(async () =>
            {
                while (!okToClearDiscoveredEndpoints) await Task.Delay(1000).ConfigureAwait(false);
                discoveredEndpoints = null;
            });

            return builder;
        }

        private static void Discover_Endpoints_Validators_EventHandlers()
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
                      !t.IsInterface &&
                      t.GetInterfaces().Intersect(new[] {
                          typeof(IEndpoint),
                          typeof(IValidator),
                          typeof(IEventHandler)
                      }).Any());
#pragma warning restore CS8602

            if (!discoveredTypes.Any())
                throw new InvalidOperationException("Unable to find any endpoint declarations!");

            //Endpoint<TRequest>
            //Validator<TRequest>

            var epList = new List<(Type tEndpoint, Type tRequest)>();

            //key: TRequest //val: TValidator
            var valDict = new Dictionary<Type, Type>();

            foreach (var type in discoveredTypes)
            {
                var interfacesOfType = type.GetInterfaces();

                if (interfacesOfType.Contains(typeof(IEventHandler)))
                {
                    ((IEventHandler?)Activator.CreateInstance(type))?.Subscribe();
                    continue;
                }

#pragma warning disable CS8602
                if (interfacesOfType.Contains(typeof(IEndpoint)))
                {
                    var tRequest = typeof(EmptyRequest);

                    if (type.BaseType?.IsGenericType is true)
                        tRequest = type.BaseType?.GetGenericArguments()?[0] ?? tRequest;

                    epList.Add((type, tRequest));
                }
                else
                {
                    Type tRequest = type.BaseType.GetGenericArguments()[0];
                    valDict.Add(tRequest, type);
                }
#pragma warning restore CS8602
            }

#pragma warning disable CS8601
            discoveredEndpoints = epList
                .Select(x => new DiscoveredEndpoint()
                {
                    EndpointType = x.tEndpoint,
                    EndpointInstance = Activator.CreateInstance(x.tEndpoint),
                    ValidatorType = GetValidatorType(x.tRequest)
                })
                .ToArray();
#pragma warning restore CS8601

            Type? GetValidatorType(Type tRequest)
            {
                Type? valType = null;
                valDict?.TryGetValue(tRequest, out valType);
                return valType;
            }
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