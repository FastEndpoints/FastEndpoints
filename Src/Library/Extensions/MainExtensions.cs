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

namespace FastEndpoints;

/// <summary>
/// provides extensions to easily bootstrap fastendpoints in the asp.net middleware pipeline
/// </summary>
public static class MainExtensions
{
    private class EndpointDefinition
    {
#pragma warning disable CS8618
        public Type EndpointType { get; set; }
        public object EndpointInstance { get; set; }
        public Type? ValidatorType { get; set; }
        public string? SecurityPolicyName { get; set; }
        public EndpointSettings Settings { get; set; }
#pragma warning restore CS8618
    }

    private static EndpointDefinition[]? discoveredEndpointDefinitions;

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

    /// <summary>
    /// finalizes auto discovery of endpoints and prepares FastEndpoints to start processing requests
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder)
    {
        if (discoveredEndpointDefinitions is null) throw new InvalidOperationException($"Please use .{nameof(AddFastEndpoints)}() first!");

        BaseEndpoint.SerializerOptions = builder.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        BaseEventHandler.ServiceProvider = builder.ServiceProvider;

        var routeToHandlerCounts = new Dictionary<string, int>();
        var logger = builder.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

        foreach (var ep in discoveredEndpointDefinitions)
        {
            var epName = ep.EndpointType.FullName;
            var epSettings = ep.Settings;

            var execMethod = ep.EndpointType.GetMethod(nameof(BaseEndpoint.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);
            if (execMethod is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{epName}]");
            if (epSettings.Verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");
            if (epSettings.Routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{epName}]");

            foreach (var route in epSettings.Routes) //for logging a warning if duplicate handlers are registered
            {
                routeToHandlerCounts.TryGetValue(route, out var count);
                routeToHandlerCounts[route] = count + 1;
            }

            var policiesToAdd = new List<string>();
            if (epSettings.Policies?.Any() is true) policiesToAdd.AddRange(epSettings.Policies);
            if (ep.SecurityPolicyName is not null) policiesToAdd.Add(ep.SecurityPolicyName);

            var epFactory = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

            EndpointExecutor.CachedServiceBoundProps[ep.EndpointType]
                = ep.EndpointType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var route in epSettings.Routes)
            {
                var eb = builder.MapMethods(route, epSettings.Verbs, EndpointExecutor.HandleAsync);

                if (epSettings.InternalConfigAction is not null) epSettings.InternalConfigAction(eb);//always do this first

                if (policiesToAdd.Count > 0)
                    eb.RequireAuthorization(policiesToAdd.ToArray());
                else
                    eb.RequireAuthorization(); //secure by default

                if (epSettings.AllowFileUploads is true) eb.Accepts<IFormFile>("multipart/form-data");
                if (epSettings.AllowAnonymous is true) eb.AllowAnonymous();

                if (epSettings.UserConfigAction is not null) epSettings.UserConfigAction(eb);//always do this last - allow user to override everything done above

                var validatorInstance = (IValidatorWithState?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));
                if (validatorInstance is not null)
                {
                    validatorInstance.ServiceProvider = builder.ServiceProvider;
                    validatorInstance.ThrowIfValidationFails = epSettings.ThrowIfValidationFails;
                }

                EndpointExecutor.CachedEndpointDefinitions[route] = new(epFactory, execMethod, validatorInstance);
            }
        }

        foreach (var kvp in routeToHandlerCounts)
            if (kvp.Value > 1) logger.LogWarning($"The route \"{kvp.Key}\" has {kvp.Value} endpoints registered to handle requests!");

        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            discoveredEndpointDefinitions = null;
        });

        return builder;
    }

    private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
    {
        if (discoveredEndpointDefinitions is null) throw new InvalidOperationException("Unable to discover any endpoint declarations!");

        foreach (var ep in discoveredEndpointDefinitions)
        {
            var eps = ep.Settings;

            if (eps.Roles is null && eps.Permissions is null && eps.Claims is null) continue;

            ep.SecurityPolicyName = $"epPolicy:{ep.EndpointType.FullName}";

            opts.AddPolicy(ep.SecurityPolicyName, b =>
            {
                if (eps.Permissions?.Any() is true)
                {
                    if (eps.AllowAnyPermission is true)
                    {
                        b.RequireAssertion(x =>
                        {
                            var hasAny = x.User.Claims
                            .FirstOrDefault(c => c.Type == Constants.PermissionsClaimType)?.Value
                            .Split(',')
                            .Intersect(eps.Permissions)
                            .Any();
                            return hasAny is true;
                        });
                    }
                    else
                    {
#pragma warning disable CS8602
                        b.RequireAssertion(x =>
                    {
                        var hasAll = !eps.Permissions
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

                if (eps.Claims?.Any() is true)
                {
                    if (eps.AllowAnyClaim is true)
                    {
                        b.RequireAssertion(x =>
                        {
                            var hasAny = x.User.Claims
                            .Select(c => c.Type)
                            .Intersect(eps.Claims)
                            .Any();
                            return hasAny is true;
                        });
                    }
                    else
                    {
                        b.RequireAssertion(x =>
                        {
                            var hasAll = !eps.Claims
                            .Except(
                                x.User.Claims
                                 .Select(c => c.Type))
                            .Any();
                            return hasAll is true;
                        });
                    }
                }

                if (eps.Roles?.Any() is true) b.RequireRole(eps.Roles);
            });
        }
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
        discoveredEndpointDefinitions = epList
            .Select(x =>
            {
                var instance = Activator.CreateInstance(x.tEndpoint);
                return new EndpointDefinition()
                {
                    EndpointType = x.tEndpoint,
                    EndpointInstance = instance,
                    ValidatorType = GetValidatorType(x.tRequest),
                    Settings = GetEndpointSettings(x.tEndpoint, instance)
                };
            })
            .ToArray();
#pragma warning restore CS8601

        Type? GetValidatorType(Type tRequest)
        {
            Type? valType = null;
            valDict?.TryGetValue(tRequest, out valType);
            return valType;
        }

        EndpointSettings GetEndpointSettings(Type epBaseType, object? epInstance)
        {
#pragma warning disable CS8600
#pragma warning disable CS8603
#pragma warning disable CS8602
            return (EndpointSettings)
                epBaseType.GetProperty(nameof(BaseEndpoint.Settings), BindingFlags.NonPublic | BindingFlags.Instance)
                          .GetValue(epInstance);
#pragma warning restore CS8602
#pragma warning restore CS8603
#pragma warning restore CS8600
        }
    }
}

internal class DuplicateHandlerRegistration { }
