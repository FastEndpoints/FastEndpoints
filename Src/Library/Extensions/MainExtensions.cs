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
    /// <param name="exclusionFilter">an optional function to exclude an endpoint from registration. return true from the function if you want to exclude an endpoint.</param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder, Func<DiscoveredEndpoint, bool>? exclusionFilter = null)
    {
        IServiceResolver.ServiceProvider = builder.ServiceProvider;
        BaseEndpoint.SerializerOptions = builder.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        //key: {verb}:{route}
        var routeToHandlerCounts = new Dictionary<string, int>();
        var totalEndpointCount = 0;

        foreach (var ep in _endpoints.Found)
        {
            var epSettings = ep.Settings;

            if (exclusionFilter?.Invoke(new(
                ep.EndpointType,
                epSettings.Routes!,
                epSettings.Verbs!,
                epSettings.AnonymousVerbs,
                epSettings.ThrowIfValidationFails,
                epSettings.PreBuiltUserPolicies,
                epSettings.Roles,
                epSettings.Permissions,
                epSettings.AllowAnyPermission,
                epSettings.ClaimTypes,
                epSettings.AllowAnyClaim
                )) is true) continue;

            var epName = ep.EndpointType.FullName;

            if (epSettings.Verbs?.Any() is not true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");
            if (epSettings.Routes?.Any() is not true) throw new ArgumentException($"No Routes declared on: [{epName}]");

            var shouldSetName = epSettings.Verbs.Length == 1 && epSettings.Routes.Length == 1;
            var epMetaData = BuildEndpointMetaData(ep);
            var policiesToAdd = BuildPoliciesToAdd(ep);

            foreach (var route in epSettings.Routes)
            {
                foreach (var verb in epSettings.Verbs)
                {
                    var hb = builder.MapMethods(route, new[] { verb }, EndpointExecutor.HandleAsync);

                    if (shouldSetName)
                        hb.WithName(epName!); //needed for link generation. only supported on single verb/route endpoints.

                    hb.WithMetadata(epMetaData); //used by EndpointExecutor on each request.

                    epSettings.InternalConfigAction(hb);//always do this first

                    if (epSettings.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(policiesToAdd.ToArray());

                    if (epSettings.ResponseCacheSettings is not null) hb.WithMetadata(epSettings.ResponseCacheSettings);
                    if (epSettings.DtoTypeForFormData is not null) hb.Accepts(epSettings.DtoTypeForFormData, "multipart/form-data");
                    if (epSettings.UserConfigAction is not null) epSettings.UserConfigAction(hb);//always do this last - allow user to override everything done above

                    var key = $"{verb}:{route}";
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
        var epInstantiator = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

        var validatorInstance = (IValidatorWithState?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));
        if (validatorInstance is not null)
            validatorInstance.ThrowIfValidationFails = ep.Settings.ThrowIfValidationFails;

        var serviceBoundReqDtoProps = ep.EndpointType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => new ServiceBoundReqDtoProp(
                p.PropertyType,
                ep.EndpointType.SetterForProp(p.Name)))
            .ToArray();

        return new EndpointMetadata(
            epInstantiator,
            validatorInstance,
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

/// <summary>
/// represents an endpoint that has been discovered during startup
/// </summary>
/// <param name="EndpointType">the type of the discovered endpoint class</param>
/// <param name="Routes">the routes the endpoint will match</param>
/// <param name="Verbs">the http verbs the endpoint will be listening for</param>
/// <param name="AnonymousVerbs">the verbs which will be allowed anonymous access to</param>
/// <param name="ThrowIfValidationFails">whether automatic validation failure will be sent</param>
/// <param name="Policies">the security policies for the endpoint</param>
/// <param name="Roles">the roles which will be allowed access to</param>
/// <param name="Permissions">the permissions which will allow access</param>
/// <param name="AllowAnyPermission">whether any or all permissions will be required</param>
/// <param name="Claims">the user claim types which will allow access</param>
/// <param name="AllowAnyClaim">whether any or all claim types will be required</param>
public record struct DiscoveredEndpoint(
    Type EndpointType,
    IEnumerable<string> Routes,
    IEnumerable<string> Verbs,
    IEnumerable<string>? AnonymousVerbs,
    bool ThrowIfValidationFails,
    IEnumerable<string>? Policies,
    IEnumerable<string>? Roles,
    IEnumerable<string>? Permissions,
    bool AllowAnyPermission,
    IEnumerable<string>? Claims,
    bool AllowAnyClaim);

internal class StartupTimer { }
internal class DuplicateHandlerRegistration { }
