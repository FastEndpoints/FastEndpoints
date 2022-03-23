using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// provides extensions to easily bootstrap fastendpoints in the asp.net middleware pipeline
/// </summary>
public static class MainExtensions
{
    private static EndpointData _endpoints;

    /// <summary>
    /// adds the FastEndpoints services to the ASP.Net middleware pipeline
    /// </summary>
    /// <param name="services"></param>
    /// <param name="endpointAssmeblies">an optional collection of additional assemblies to discover endpoints from</param>
    public static IServiceCollection AddFastEndpoints(this IServiceCollection services, IEnumerable<Assembly>? endpointAssmeblies = null)
    {
        _endpoints = new(services, endpointAssmeblies);
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

    public static IApplicationBuilder UseFastEndpointsMiddleware(this IApplicationBuilder app)
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
            if (epDef.Verbs?.Any() is not true) throw new ArgumentException($"No HTTP Verbs declared on: [{epDef.EndpointType.FullName}]");
            if (epDef.Routes?.Any() is not true) throw new ArgumentException($"No Routes declared on: [{epDef.EndpointType.FullName}]");

            var authorizeAttributes = BuildAuthorizeAttributes(epDef);
            var routeNum = 0;

            foreach (var route in epDef.Routes)
            {
                var finalRoute = routeBuilder.BuildRoute(epDef.Version.Current, route, epDef.RoutePrefixOverride);
                IEndpoint.SetTestURL(epDef.EndpointType, finalRoute);

                routeNum++;

                foreach (var verb in epDef.Verbs)
                {
                    var hb = app.MapMethods(finalRoute, new[] { verb }, SendMisconfiguredPipelineMsg());

                    hb.WithName(
                        epDef.EndpointType.EndpointName(
                            epDef.Verbs.Length > 1 ? verb : null,
                            epDef.Routes.Length > 1 ? routeNum : null)); //user can override this via Options(x=>x.WithName(...))

                    hb.WithMetadata(epDef);

                    epDef.InternalConfigAction(hb); //always do this first here

                    if (epDef.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(authorizeAttributes);

                    if (epDef.ResponseCacheSettings is not null)
                        hb.WithMetadata(epDef.ResponseCacheSettings);

                    if (epDef.AllowFormData)
                        hb.Accepts(epDef.ReqDtoType, "multipart/form-data");

                    GlobalEpOptsAction?.Invoke(epDef, hb);

                    epDef.UserConfigAction?.Invoke(hb);//always do this last - allow user to override everything done above

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

        var duplicatesDetected = false;
        var logger = IServiceResolver.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

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
            //release memory held by _endpoints static variable after 10 mins as it's not needed after app startup.
            //we wait for 10 minutes in case WAF might create multiple instances of the web application in some testing scenarios.
            //if someone's tests run for more than 10 minutes, we should make this a user configurable setting.

            await Task.Delay(TimeSpan.FromMinutes(10));
            _endpoints = null!;
        });

        return app;
    }

    private static Func<string> SendMisconfiguredPipelineMsg()
        => () => "UseFastEndpoints() must appear after any routing middleware like UseRouting() and before any terminating middleware like UseEndpoints()";

    internal static string BuildRoute(this StringBuilder builder, int epVersion, string route, string? prefixOverride)
    {
        // {rPrfix}/{p}{ver}/{route}
        // mobile/v1/customer/retrieve

        // {rPrfix}/{route}/{p}{ver}
        // mobile/customer/retrieve/v1

        if (RoutingOpts?.Prefix is not null && prefixOverride != string.Empty)
        {
            builder.Append('/')
                   .Append(prefixOverride ?? RoutingOpts.Prefix)
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

    private static IAuthorizeData[] BuildAuthorizeAttributes(EndpointDefinition ep)
    {
        var policiesToAdd = new List<string>();

        if (ep.PreBuiltUserPolicies?.Any() is true)
            policiesToAdd.AddRange(ep.PreBuiltUserPolicies);

        if (ep.Permissions?.Any() is true ||
            ep.ClaimTypes?.Any() is true ||
            ep.Roles?.Any() is true ||
            ep.AuthSchemes?.Any() is true)
        {
            policiesToAdd.Add(ep.SecurityPolicyName);
        }

        return policiesToAdd.Select(p =>
        {
            var attr = new AuthorizeAttribute { Policy = p, };

            if (ep.AuthSchemes is not null)
                attr.AuthenticationSchemes = string.Join(',', ep.AuthSchemes);

            if (ep.Roles is not null)
                attr.Roles = string.Join(',', ep.Roles);

            return attr;
        }).ToArray();
    }

    private static void BuildSecurityPoliciesForEndpoints(AuthorizationOptions opts)
    {
        foreach (var ep in _endpoints.Found)
        {
            if (ep.Roles is null && ep.Permissions is null && ep.ClaimTypes is null && ep.AuthSchemes is null)
                continue;

            opts.AddPolicy(ep.SecurityPolicyName, b =>
            {
                b.RequireAuthenticatedUser();

                if (ep.Permissions?.Length > 0)
                {
                    if (ep.AllowAnyPermission)
                    {
                        b.RequireAssertion(x =>
                            x.User.Claims.Any(c =>
                                string.Equals(c.Type, Constants.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                ep.Permissions.Contains(c.Value, StringComparer.Ordinal)));
                    }
                    else
                    {
                        b.RequireAssertion(x =>
                            ep.Permissions.All(p =>
                                x.User.Claims.Any(c =>
                                    string.Equals(c.Type, Constants.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(c.Value, p, StringComparison.Ordinal))));
                    }
                }

                if (ep.ClaimTypes?.Length > 0)
                {
                    if (ep.AllowAnyClaim)
                    {
                        b.RequireAssertion(x =>
                            x.User.Claims.Any(c =>
                                ep.ClaimTypes.Contains(c.Type, StringComparer.OrdinalIgnoreCase)));
                    }
                    else
                    {
                        b.RequireAssertion(x =>
                            ep.ClaimTypes.All(t =>
                                x.User.Claims.Any(c =>
                                    string.Equals(c.Type, t, StringComparison.OrdinalIgnoreCase))));
                    }
                }

                //note: only claim and permission requirements are added here in the security policy
                //      roles and auth schemes are specified in the authorizeattribute in BuildAuthorizeAttributes()
            });
        }
    }
}

internal class StartupTimer { }
internal class DuplicateHandlerRegistration { }