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
using System.Text;
using static FastEndpoints.Config;

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
    /// <para>HINT: this is the combination of <c>app.UseFastEndpointsMiddleware()</c> and <c>app.MapFastEndpoints()</c>.
    /// you can use those two methods separately if you have some special requirement such as using "Startup.cs", etc.
    /// </para>
    /// </summary>
    /// <param name="configAction">an optional action to configure FastEndpoints</param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static WebApplication UseFastEndpoints(this WebApplication app, Action<Config>? configAction = null)
    {
        UseFastEndpointsMiddleware(app);
        MapFastEndpoints(app, configAction);
        return app;
    }

    public static IApplicationBuilder UseFastEndpointsMiddleware(IApplicationBuilder app)
    {
        app.UseMiddleware<ExecutorMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapFastEndpoints(this IEndpointRouteBuilder app, Action<Config>? configAction = null)
    {
        IServiceResolver.ServiceProvider = app.ServiceProvider;
        SerializerOpts = IServiceResolver.ServiceProvider.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions ?? SerializerOpts;
        configAction?.Invoke(new Config());

        //key: {verb}:{route}
        var routeToHandlerCounts = new Dictionary<string, int>();
        var totalEndpointCount = 0;
        var routeBuilder = new StringBuilder();

        foreach (var epDef in _endpoints.Found)
        {
            if (EpRegFilterFunc is not null && !EpRegFilterFunc(epDef)) continue;
            if (epDef.Settings.Verbs?.Any() is not true) throw new ArgumentException($"No HTTP Verbs declared on: [{epDef.EndpointType.FullName}]");
            if (epDef.Settings.Routes?.Any() is not true) throw new ArgumentException($"No Routes declared on: [{epDef.EndpointType.FullName}]");

            var shouldSetName = epDef.Settings.Verbs.Length == 1 && epDef.Settings.Routes.Length == 1;
            var epMetaData = BuildEndpointMetaData(epDef);
            var authorizeAttributes = BuildAuthorizeAttributes(epDef);

            foreach (var route in epDef.Settings.Routes)
            {
                var finalRoute = routeBuilder.BuildRoute(epDef.Settings.Version.Current, route);

                foreach (var verb in epDef.Settings.Verbs)
                {
                    var hb = app.MapMethods(finalRoute, new[] { verb }, SendMisconfiguredPipelineMsg());

                    if (shouldSetName)
                        hb.WithName(epDef.EndpointType.SantizedName()); //needed for link generation. only supported on single verb/route endpoints.

                    hb.WithMetadata(epMetaData, epDef);

                    epDef.Settings.InternalConfigAction(hb);//always do this first                    

                    if (epDef.Settings.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(authorizeAttributes);

                    if (epDef.Settings.ResponseCacheSettings is not null)
                        hb.WithMetadata(epDef.Settings.ResponseCacheSettings);

                    if (epDef.Settings.DtoTypeForFormData is not null)
                        hb.Accepts(epDef.Settings.DtoTypeForFormData, "multipart/form-data");

                    epDef.Settings.UserConfigAction?.Invoke(hb);//always do this last - allow user to override everything done above

                    var key = $"{verb}:{finalRoute}";
                    routeToHandlerCounts.TryGetValue(key, out var count);
                    routeToHandlerCounts[key] = count + 1;
                    totalEndpointCount++;
                }
            }
        }

        IServiceResolver.ServiceProvider.GetRequiredService<ILogger<StartupTimer>>().LogInformation(
            $"Registered {totalEndpointCount} endpoints in " +
            $"{EndpointData.Stopwatch.ElapsedMilliseconds:0} milliseconds.");

        EndpointData.Stopwatch.Stop();

        var logger = IServiceResolver.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

        bool duplicatesDetected = false;

        foreach (var kvp in routeToHandlerCounts)
        {
            if (kvp.Value > 1)
            {
                duplicatesDetected = true;
                logger.LogError($"The route \"{kvp.Key}\" has {kvp.Value} endpoints registered to handle requests!");
            }
        }

        if (duplicatesDetected)
            throw new InvalidOperationException("Duplicate routes detected! See log for more details.");

        Task.Run(async () =>
        {
            //release memory held by endpointData after 10 mins as it's not needed after app startup.
            //we wait for 10 minutes in case WAF might create multiple instances of the web application in some testing scenarios.
            //if someone's tests run for more than 10 minutes, we should make this a user configurable setting.

            await Task.Delay(TimeSpan.FromMinutes(10)).ConfigureAwait(false);
            _endpoints = null!;
        });

        return app;
    }

    private static Func<string> SendMisconfiguredPipelineMsg()
        => () => "UseFastEndpoints() must appear after any routing middleware like UseRouting() and before any terminating middleware like UseEndpoints()";

    internal static string BuildRoute(this StringBuilder builder, int epVersion, string route)
    {
        // {rPrfix}/{p}{ver}/{route}
        // mobile/v1/customer/retrieve

        // {rPrfix}/{route}/{p}{ver}
        // mobile/customer/retrieve/v1

        if (RoutingOpts?.Prefix is not null)
        {
            builder.Append('/')
                   .Append(RoutingOpts.Prefix)
                   .Append('/');
        }

        if (VersioningOpts?.SuffixedVersion is false)
            AppendVersion(builder, epVersion, trailingSlash: true);

        if (builder.Length > 0 && route.StartsWith('/'))
            builder.Remove(builder.Length - 1, 1);

        builder.Append(route);

        if (VersioningOpts?.SuffixedVersion is true)
            AppendVersion(builder, epVersion, trailingSlash: false);

        var final = builder.ToString();
        builder.Clear();
        return final;

        static void AppendVersion(StringBuilder builder, int epVersion, bool trailingSlash)
        {
            if (epVersion > 0)
            {
                if (builder.Length > 0 && builder[^1] != '/')
                    builder.Append('/');

                builder.Append(VersioningOpts!.Prefix)
                       .Append(epVersion);

                if (trailingSlash) builder.Append('/');
            }
            else if (VersioningOpts?.DefaultVersion != 0)
            {
                if (builder.Length > 0 && builder[^1] != '/')
                    builder.Append('/');

                builder.Append(VersioningOpts!.Prefix)
                       .Append(VersioningOpts!.DefaultVersion);

                if (trailingSlash) builder.Append('/');
            }
        }
    }

    internal static string SantizedName(this Type type) => type.FullName?.Replace(".", string.Empty)!;

    private static IAuthorizeData[] BuildAuthorizeAttributes(EndpointDefinition ep)
    {
        var policiesToAdd = new List<string>();

        if (ep.Settings.PreBuiltUserPolicies?.Any() is true) policiesToAdd.AddRange(ep.Settings.PreBuiltUserPolicies);

        if (ep.Settings.Permissions?.Any() is true ||
            ep.Settings.ClaimTypes?.Any() is true ||
            ep.Settings.Roles?.Any() is true ||
            ep.Settings.AuthSchemes?.Any() is true)
        {
            policiesToAdd.Add(SecurityPolicyName(ep.EndpointType));
        }

        return policiesToAdd.Select(p =>
        {
            var attr = new AuthorizeAttribute { Policy = p, };

            if (ep.Settings.AuthSchemes is not null)
                attr.AuthenticationSchemes = string.Join(',', ep.Settings.AuthSchemes);

            if (ep.Settings.Roles is not null)
                attr.Roles = string.Join(',', ep.Settings.Roles);

            return attr;
        }).ToArray();
    }

    private static EndpointMetadata BuildEndpointMetaData(EndpointDefinition ep)
    {
        var validator = (IValidatorWithState?)(ep.ValidatorType is null ? null : Activator.CreateInstance(ep.ValidatorType));
        if (validator is not null)
            validator.ThrowIfValidationFails = ep.Settings.ThrowIfValidationFails;

        var serviceBoundEpProps = ep.EndpointType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => new ServiceBoundEpProp(
                p.PropertyType,
                ep.EndpointType.SetterForProp(p.Name)))
            .ToArray();

        return new EndpointMetadata(
            Expression.Lambda<Func<object>>(Expression.New(ep.EndpointType)).Compile(),
            validator,
            serviceBoundEpProps,
            ep.Settings);
    }

    private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
    {
        foreach (var ep in _endpoints.Found)
        {
            var eps = ep.Settings;

            if (eps.Roles is null && eps.Permissions is null && eps.ClaimTypes is null && eps.AuthSchemes is null)
                continue;

            var secPolName = SecurityPolicyName(ep.EndpointType);

            opts.AddPolicy(secPolName, b =>
            {
                b.RequireAuthenticatedUser();

                if (eps.Permissions?.Any() is true)
                {
                    if (eps.AllowAnyPermission)
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
                    if (eps.AllowAnyClaim)
                        b.RequireAssertion(x => x.User.Claims.Select(c => c.Type).Intersect(eps.ClaimTypes).Any());
                    else
                        b.RequireAssertion(x => !eps.ClaimTypes.Except(x.User.Claims.Select(c => c.Type)).Any());
                }

                //note: only claim and permission requirements are added here in the security policy
                //      roles and auth schemes are specified in the authorizeattribute in BuildAuthorizeAttributes()
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