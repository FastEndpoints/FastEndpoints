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
    private static EndpointData endpointData = new();

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
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IEndpointRouteBuilder UseFastEndpoints(this IEndpointRouteBuilder builder)
    {
        IServiceResolver.ServiceProvider = builder.ServiceProvider;
        BaseEndpoint.SerializerOptions = builder.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        //key: {verb}:{route}
        var routeToHandlerCounts = new Dictionary<string, int>();
        var totalEndpointCount = 0;

        foreach (var ep in endpointData.Definitions)
        {
            var epName = ep.EndpointType.FullName;
            var epSettings = ep.Settings;

            if (epSettings.Verbs?.Any() is not true) throw new ArgumentException($"No HTTP Verbs declared on: [{epName}]");
            if (epSettings.Routes?.Any() is not true) throw new ArgumentException($"No Routes declared on: [{epName}]");

            var shouldSetName = epSettings.Verbs.Length == 1 && epSettings.Routes.Length == 1;
            var epMetaData = BuildEndpointMetaData(ep);
            var policiesToAdd = BuildPoliciesToAdd(ep);

            foreach (var route in epSettings.Routes)
            {
                foreach (var verb in epSettings.Verbs)
                {
                    var eb = builder.MapMethods(route, new[] { verb }, EndpointExecutor.HandleAsync);

                    if (shouldSetName)
                        eb.WithName(epName!); //needed for link generation. only supported on single verb/route endpoints.

                    eb.WithMetadata(epMetaData); //used by EndpointExecutor on each request.

                    epSettings.InternalConfigAction(eb);//always do this first

                    if (epSettings.AnonymousVerbs?.Contains(verb) is true)
                        eb.AllowAnonymous();
                    else
                        eb.RequireAuthorization(policiesToAdd.ToArray());

                    if (epSettings.ResponseCacheSettings is not null) eb.WithMetadata(epSettings.ResponseCacheSettings);
                    if (epSettings.DtoTypeForFormData is not null) eb.Accepts(epSettings.DtoTypeForFormData, "multipart/form-data");
                    if (epSettings.UserConfigAction is not null) epSettings.UserConfigAction(eb);//always do this last - allow user to override everything done above

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
            endpointData = null!;
        });

        return builder;
    }

    private static List<string> BuildPoliciesToAdd(EndpointDefinition ep)
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

    private static EndpointMetadata BuildEndpointMetaData(EndpointDefinition ep)
    {
        var epFactory = Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile();

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
            epFactory,
            validatorInstance,
            serviceBoundReqDtoProps,
            ep.Settings.PreProcessors,
            ep.Settings.PostProcessors);
    }

    private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
    {
        foreach (var ep in endpointData.Definitions)
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
