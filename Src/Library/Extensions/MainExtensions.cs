using FastEndpoints.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                var fieldInfo = ep.EndpointType.BaseType?.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

                var roles = fieldInfo?.GetValues(nameof(BaseEndpoint.roles), ep.EndpointInstance);
                var permissions = fieldInfo?.GetValues(nameof(BaseEndpoint.permissions), ep.EndpointInstance);
                var allowAnyPermission = fieldInfo?.GetValue<bool>(nameof(BaseEndpoint.allowAnyPermission), ep.EndpointInstance);
                var claims = fieldInfo?.GetValues(nameof(BaseEndpoint.claims), ep.EndpointInstance);
                var allowAnyClaim = fieldInfo?.GetValue<bool>(nameof(BaseEndpoint.allowAnyClaim), ep.EndpointInstance);

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

            var routeToHandlerCounts = new Dictionary<string, int>();
            var logger = builder.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

            foreach (var ep in discoveredEndpoints)
            {
                var epName = ep.EndpointType.FullName;

                var execMethod = ep.EndpointType.GetMethod(nameof(BaseEndpoint.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);
                if (execMethod is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{epName}]");

                var fieldInfo = ep.EndpointType.BaseType?.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

                var verbs = fieldInfo?.GetValues(nameof(BaseEndpoint.verbs), ep.EndpointInstance);
                if (verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");

                var routes = fieldInfo?.GetValues(nameof(BaseEndpoint.routes), ep.EndpointInstance);
                if (routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{epName}]");

                foreach (var route in routes) //for logging a warning if duplicate handlers are registered
                {
                    routeToHandlerCounts.TryGetValue(route, out var count);
                    routeToHandlerCounts[route] = count + 1;
                }

                var userPolicies = fieldInfo?.GetValues(nameof(BaseEndpoint.policies), ep.EndpointInstance);
                var policiesToAdd = new List<string>();
                if (userPolicies?.Any() is true) policiesToAdd.AddRange(userPolicies);
                if (ep.SecurityPolicyName is not null) policiesToAdd.Add(ep.SecurityPolicyName);

                var intConfigAction = fieldInfo?.GetValue<Action<DelegateEndpointConventionBuilder>?>(nameof(BaseEndpoint.internalConfigAction), ep.EndpointInstance);
                var usrConfigAction = fieldInfo?.GetValue<Action<DelegateEndpointConventionBuilder>?>(nameof(BaseEndpoint.userConfigAction), ep.EndpointInstance);
                var allowAnnonymous = fieldInfo?.GetValue<bool>(nameof(BaseEndpoint.allowAnnonymous), ep.EndpointInstance);
                var allowFileUpload = fieldInfo?.GetValue<bool>(nameof(BaseEndpoint.allowFileUploads), ep.EndpointInstance);

                var epFactory = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

                EndpointExecutor.CachedServiceBoundProps[ep.EndpointType]
                    = ep.EndpointType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var route in routes)
                {
                    var eb = builder.MapMethods(route, verbs, EndpointExecutor.HandleAsync);

                    if (intConfigAction is not null) intConfigAction(eb);//always do this first

                    if (policiesToAdd.Count > 0)
                        eb.RequireAuthorization(policiesToAdd.ToArray());
                    else
                        eb.RequireAuthorization(); //secure by default

                    if (allowFileUpload is true) eb.Accepts<IFormFile>("multipart/form-data");
                    if (allowAnnonymous is true) eb.AllowAnonymous();

                    if (usrConfigAction is not null) usrConfigAction(eb);//always do this last - allow user to override everything done above

                    var validatorInstance = (IValidator?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));

                    EndpointExecutor.CachedEndpointTypes[route] = (epFactory, execMethod, validatorInstance);
                }
            }

            foreach (var kvp in routeToHandlerCounts)
                if (kvp.Value > 1) logger.LogWarning($"The route \"{kvp.Key}\" has {kvp.Value} endpoints registered to handle requests!");

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

        private static IEnumerable<string>? GetValues(this FieldInfo[] fields, string fieldName, object endpointInstance)
            => (IEnumerable<string>?)fields.Single(f => f.Name == fieldName).GetValue(endpointInstance);

        private static TOut? GetValue<TOut>(this FieldInfo[] fields, string fieldName, object endpointInstance)
            => (TOut?)fields.Single(f => f.Name == fieldName).GetValue(endpointInstance);
    }

    internal class DuplicateHandlerRegistration { }
}