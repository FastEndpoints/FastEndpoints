﻿using FastEndpoints.Validation;
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
    private static EndpointData _endpoints = new();

    /// <summary>
    /// adds the FastEndpoints services to the ASP.Net middleware pipeline
    /// </summary>
    /// <param name="services"></param>
    public static IServiceCollection AddFastEndpoints(this IServiceCollection services)
    {
        services.AddAuthorization(BuildSecurityPoliciesForEndpoints); //this method doesn't block
        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>
    /// finalizes auto discovery of endpoints and prepares FastEndpoints to start processing requests
    /// </summary>
    /// <param name="configAction">an optional action to configure FastEndpoints</param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder, Action<Config>? configAction = null)
    {
        IServiceResolver.ServiceProvider = builder.ServiceProvider;
        Config.serializerOptions = builder.ServiceProvider.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions ?? Config.serializerOptions;
        configAction?.Invoke(new Config());

        //key: {verb}:{route}
        var routeToHandlerCounts = new Dictionary<string, int>();
        var totalEndpointCount = 0;

        foreach (var ep in _endpoints.Found)
        {
            if (Config.endpointRegistrationFilter is not null && !Config.endpointRegistrationFilter(CreateDiscoverdEndpoint(ep))) continue;
            if (ep.Settings.Verbs?.Any() is not true) throw new ArgumentException($"No HTTP Verbs declared on: [{ep.EndpointType.FullName}]");
            if (ep.Settings.Routes?.Any() is not true) throw new ArgumentException($"No Routes declared on: [{ep.EndpointType.FullName}]");

            var shouldSetName = ep.Settings.Verbs.Length == 1 && ep.Settings.Routes.Length == 1;
            var epMetaData = BuildEndpointMetaData(ep);
            var policiesToAdd = BuildPoliciesToAdd(ep);

            foreach (var route in ep.Settings.Routes)
            {
                foreach (var verb in ep.Settings.Verbs)
                {
                    var targetRoute = route;
                    if (Config.versioningOptions is not null)
                    {
                        var needSlash = !targetRoute.StartsWith("/");
                        var versionPrefix = $"{Config.versioningOptions.Prefix}{ep.Settings.Version ?? Config.versioningOptions.DefaultVersion}{(needSlash ? "/" : "")}";
                        targetRoute = $"{versionPrefix}{targetRoute}";
                    }

                    if (Config.routingOptions is not null)
                    {
                        if (!string.IsNullOrEmpty(Config.routingOptions.Prefix))
                        {
                            var needSlash = !targetRoute.StartsWith("/");
                            targetRoute = $"{Config.routingOptions.Prefix}{(needSlash ? "/" : "")}{targetRoute}";
                        }
                    }
                    
                    var hb = builder.MapMethods(targetRoute, new[] { verb }, EndpointExecutor.HandleAsync);

                    if (shouldSetName)
                        hb.WithName(ep.EndpointType.FullName!); //needed for link generation. only supported on single verb/route endpoints.

                    hb.WithMetadata(epMetaData);
                    ep.Settings.InternalConfigAction(hb);//always do this first

                    if (ep.Settings.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(policiesToAdd.ToArray());

                    if (ep.Settings.ResponseCacheSettings is not null) hb.WithMetadata(ep.Settings.ResponseCacheSettings);
                    if (ep.Settings.DtoTypeForFormData is not null) hb.Accepts(ep.Settings.DtoTypeForFormData, "multipart/form-data");
                    if (ep.Settings.UserConfigAction is not null) ep.Settings.UserConfigAction(hb);//always do this last - allow user to override everything done above

                    var key = $"{verb}:{targetRoute}";
                    routeToHandlerCounts.TryGetValue(key, out var count);
                    routeToHandlerCounts[key] = count + 1;
                    totalEndpointCount++;
                }
            }
        }

        builder.ServiceProvider.GetRequiredService<ILogger<StartupTimer>>().LogInformation(
            $"Registered {totalEndpointCount} endpoints in " +
            $"{EndpointData.Stopwatch.ElapsedMilliseconds:0} milliseconds.");

        EndpointData.Stopwatch.Stop();

        var logger = builder.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

        foreach (var kvp in routeToHandlerCounts)
            if (kvp.Value > 1) logger.LogError($"The route \"{kvp.Key}\" has {kvp.Value} endpoints registered to handle requests!");

        Task.Run(async () =>
        {
            //release memory held by endpointData after 10 mins as it's not needed after app startup.
            //we wait for 10 minutes in case WAF might create multiple instances of the web application in some testing scenarios.
            //if someone's tests run for more than 10 minutes, we should make this a user configurable setting.

            await Task.Delay(TimeSpan.FromMinutes(10)).ConfigureAwait(false);
            _endpoints = null!;
        });

        return builder;
    }

    private static DiscoveredEndpoint CreateDiscoverdEndpoint(FoundEndpoint ep) => new(
        ep.EndpointType,
        ep.Settings.Routes!,
        ep.Settings.Verbs!,
        ep.Settings.AnonymousVerbs,
        ep.Settings.ThrowIfValidationFails,
        ep.Settings.PreBuiltUserPolicies,
        ep.Settings.Roles,
        ep.Settings.Permissions,
        ep.Settings.AllowAnyPermission,
        ep.Settings.ClaimTypes,
        ep.Settings.AllowAnyClaim,
        ep.Settings.Tags);

    private static List<string> BuildPoliciesToAdd(FoundEndpoint ep)
    {
        var policiesToAdd = new List<string>();
        if (ep.Settings.PreBuiltUserPolicies?.Any() is true) policiesToAdd.AddRange(ep.Settings.PreBuiltUserPolicies);
        if (ep.Settings.Permissions?.Any() is true ||
            ep.Settings.ClaimTypes?.Any() is true ||
            ep.Settings.Roles?.Any() is true)
        {
            policiesToAdd.Add(SecurityPolicyName(ep.EndpointType));
        }
        return policiesToAdd;
    }

    private static EndpointMetadata BuildEndpointMetaData(FoundEndpoint ep)
    {
        var validator = (IValidatorWithState?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));
        if (validator is not null)
            validator.ThrowIfValidationFails = ep.Settings.ThrowIfValidationFails;

        var serviceBoundReqDtoProps = ep.EndpointType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => new ServiceBoundReqDtoProp(
                p.PropertyType,
                ep.EndpointType.SetterForProp(p.Name)))
            .ToArray();

        return new EndpointMetadata(
            Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile(),
            validator,
            serviceBoundReqDtoProps,
            ep.Settings.PreProcessors,
            ep.Settings.PostProcessors);
    }

    private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
    {
        foreach (var ep in _endpoints.Found)
        {
            var eps = ep.Settings;

            if (eps.Roles is null && eps.Permissions is null && eps.ClaimTypes is null) continue;

            var secPolName = SecurityPolicyName(ep.EndpointType);

            opts.AddPolicy(secPolName, b =>
            {
                b.RequireAuthenticatedUser();

                if (eps.Permissions?.Any() is true)
                {
                    if (eps.AllowAnyPermission is true)
                    {
                        b.RequireAssertion(x =>
                        {
                            var prmClaimVals = x.User.FindAll(Constants.PermissionsClaimType).Select(c => c.Value);
                            if (!prmClaimVals.Any()) return false;
                            return prmClaimVals.Intersect(eps.Permissions).Any();
                        });
                    }
                    else
                    {
                        b.RequireAssertion(x =>
                        {
                            var prmClaimVals = x.User.FindAll(Constants.PermissionsClaimType).Select(c => c.Value);
                            if (!prmClaimVals.Any()) return false;
                            return !eps.Permissions.Except(prmClaimVals).Any();
                        });
                    }
                }

                if (eps.ClaimTypes?.Any() is true)
                {
                    if (eps.AllowAnyClaim is true)
                        b.RequireAssertion(x => x.User.Claims.Select(c => c.Type).Intersect(eps.ClaimTypes).Any());
                    else
                        b.RequireAssertion(x => !eps.ClaimTypes.Except(x.User.Claims.Select(c => c.Type)).Any());
                }

                if (eps.Roles?.Any() is true) b.RequireRole(eps.Roles);
            });
        }
    }

    private static string SecurityPolicyName(Type endpointType)
    {
        return $"epPolicy:{endpointType.FullName}";
    }
}

internal class StartupTimer { }
internal class DuplicateHandlerRegistration { }