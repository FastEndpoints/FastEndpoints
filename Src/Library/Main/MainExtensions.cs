﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FastEndpoints;

/// <summary>
/// provides extensions to easily bootstrap fastendpoints in the asp.net middleware pipeline
/// </summary>
public static class MainExtensions
{
    /// <summary>
    /// adds the FastEndpoints services to the ASP.Net middleware pipeline
    /// </summary>
    /// <param name="options">optionally specify the endpoint discovery options</param>
    public static IServiceCollection AddFastEndpoints(this IServiceCollection services, Action<EndpointDiscoveryOptions>? options = null)
    {
        var opts = new EndpointDiscoveryOptions();
        options?.Invoke(opts);
        var cmdHandlerRegistry = new CommandHandlerRegistry();
        var endpointData = new EndpointData(opts, cmdHandlerRegistry);
        services.AddSingleton(cmdHandlerRegistry);
        services.AddSingleton(endpointData);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton<IEndpointFactory, EndpointFactory>();
        services.TryAddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
        services.AddSingleton(typeof(EventBus<>));

        return services;
    }

    /// <summary>
    /// finalizes auto discovery of endpoints and prepares FastEndpoints to start processing requests
    /// <para>
    /// HINT: you can use <see cref="MapFastEndpoints(IEndpointRouteBuilder, Action{Config}?)" /> instead of this method if you have some special
    /// requirement such as using "Startup.cs", etc.
    /// </para>
    /// </summary>
    /// <param name="configAction">an optional action to configure FastEndpoints</param>
    /// <exception cref="InvalidCastException">thrown when the <c>app</c> cannot be cast to <see cref="IEndpointRouteBuilder" /></exception>
    public static IApplicationBuilder UseFastEndpoints(this IApplicationBuilder app, Action<Config>? configAction = null)
    {
        if (app is not IEndpointRouteBuilder routeBuilder)
            throw new InvalidCastException($"Cannot cast [{nameof(app)}] to IEndpointRouteBuilder");

        MapFastEndpoints(routeBuilder, configAction);

        return app;
    }

    public static IEndpointRouteBuilder MapFastEndpoints(this IEndpointRouteBuilder app, Action<Config>? configAction = null)
    {
        Conf.ServiceResolver = app.ServiceProvider.GetRequiredService<IServiceResolver>();
        var jsonOpts = app.ServiceProvider.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions;
        Conf.SerOpts.Options = jsonOpts is not null
                                   ? new(jsonOpts) //make a copy to avoid configAction modifying the global JsonOptions
                                   : Conf.SerOpts.Options;
        configAction?.Invoke(new());

        var endpoints = app.ServiceProvider.GetRequiredService<EndpointData>();
        var epFactory = app.ServiceProvider.GetRequiredService<IEndpointFactory>();
        var authOptions = app.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        using var scope = app.ServiceProvider.CreateScope();
        var httpCtx = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; //only because endpoint factory requires the service provider
        var routeToHandlerCounts = new ConcurrentDictionary<string, int>();               //key: {verb}:{route}
        var totalEndpointCount = 0;
        var routeBuilder = new StringBuilder();

        foreach (var def in endpoints.Found)
        {
            var ep = epFactory.Create(def, httpCtx);
            def.Initialize(ep, httpCtx);

            if (Conf.EpOpts.Filter is not null && !Conf.EpOpts.Filter(def))
                continue;

            if (def.Verbs?.Any() is not true)
                throw new ArgumentException($"No HTTP Verbs declared on: [{def.EndpointType.FullName}]");
            if (def.Routes?.Any() is not true)
                throw new ArgumentException($"No Routes declared on: [{def.EndpointType.FullName}]");

            Conf.EpOpts.Configurator?.Invoke(def); //apply global ep settings to the definition

            AddSecurityPolicy(authOptions, def);

            var authorizeAttributes = BuildAuthorizeAttributes(def);
            var routeNum = 0;

            foreach (var route in def.Routes)
            {
                var finalRoute = routeBuilder.BuildRoute(def.Version.Current, route, def.OverriddenRoutePrefix);
                IEndpoint.SetTestUrl(def.EndpointType, finalRoute);

                routeNum++;

                foreach (var verb in def.Verbs)
                {
                    var hb = app.MapMethods(
                        finalRoute,
                        new[] { verb },
                        (HttpContext ctx, [FromServices] IEndpointFactory factory) => RequestHandler.Invoke(ctx, factory));

                    hb.WithName(
                        def.EndpointType.EndpointName(
                            def.Verbs.Length > 1 ? verb : null,
                            def.Routes.Length > 1 ? routeNum : null)); //user can override this via Options(x=>x.WithName(...))

                    hb.WithMetadata(def);

                    def.InternalConfigAction(hb); //always do this first here

                    if (def.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(authorizeAttributes);

                    if (def.ResponseCacheSettings is not null)
                        hb.WithMetadata(def.ResponseCacheSettings);

                    if (def.FormDataContentType is not null)
                        hb.Accepts(def.ReqDtoType, def.FormDataContentType);

                    if (def.EndpointSummary?.ProducesMetas.Count > 0)
                    {
                        EndpointSummary.ClearDefaultProduces200Metadata(hb);
                        foreach (var pMeta in def.EndpointSummary.ProducesMetas)
                            hb.WithMetadata(pMeta);
                    }

                    def.UserConfigAction?.Invoke(hb); //always do this last - allow user to override everything done above

                    var key = $"{verb}:{finalRoute}";
                    routeToHandlerCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
                    totalEndpointCount++;
                }
            }
        }

        app.ServiceProvider.GetRequiredService<ILogger<StartupTimer>>().LogInformation(
            $"Registered {totalEndpointCount} endpoints in " +
            $"{endpoints.Stopwatch.ElapsedMilliseconds:0} milliseconds.");

        endpoints.Stopwatch.Stop();

        if (!Conf.VerOpts.IsUsingAspVersioning)
        {
            var duplicatesDetected = false;
            var logger = app.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

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
        }

        CommandExtensions.TestHandlersPresent = app.ServiceProvider.GetService<TestCommandHandlerMarker>() is not null;

        return app;
    }

    internal static string BuildRoute(this StringBuilder builder, int epVersion, string route, string? prefixOverride)
    {
        // {rPrfix}/{p}{ver}/{route}
        // mobile/v1/customer/retrieve

        // {rPrfix}/{route}/{p}{ver}
        // mobile/customer/retrieve/v1

        if (Conf.EpOpts.RoutePrefix is not null && prefixOverride != string.Empty)
        {
            builder.Append('/')
                   .Append(prefixOverride ?? Conf.EpOpts.RoutePrefix)
                   .Append('/');
        }

        if (Conf.VerOpts.PrependToRoute is true)
            AppendVersion(builder, epVersion, trailingSlash: true);

        if (builder.Length > 0 && route.StartsWith('/'))
            builder.Remove(builder.Length - 1, 1);

        builder.Append(route);

        if (Conf.VerOpts.PrependToRoute is not true)
            AppendVersion(builder, epVersion, trailingSlash: false);

        var final = builder.ToString();
        builder.Clear();

        return final;

        static void AppendVersion(StringBuilder builder, int epVersion, bool trailingSlash)
        {
            var prefix = Conf.VerOpts.Prefix ?? "v";

            if (epVersion > 0)
            {
                if (builder.Length > 0 && builder[^1] != '/')
                    builder.Append('/');

                builder.Append(prefix)
                       .Append(epVersion);

                if (trailingSlash)
                    builder.Append('/');
            }
            else if (Conf.VerOpts.DefaultVersion != 0)
            {
                if (builder.Length > 0 && builder[^1] != '/')
                    builder.Append('/');

                builder.Append(prefix)
                       .Append(Conf.VerOpts.DefaultVersion);

                if (trailingSlash)
                    builder.Append('/');
            }
        }
    }

    static IAuthorizeData[] BuildAuthorizeAttributes(EndpointDefinition ep)
    {
        var policiesToAdd = new List<string>();

        if (ep.PreBuiltUserPolicies?.Any() is true)
            policiesToAdd.AddRange(ep.PreBuiltUserPolicies);

        if (ep.RequiresAuthorization())
            policiesToAdd.Add(ep.SecurityPolicyName);

        return policiesToAdd.Select(
            p =>
            {
                var attr = new AuthorizeAttribute { Policy = p };

                if (ep.AuthSchemeNames is not null)
                    attr.AuthenticationSchemes = string.Join(',', ep.AuthSchemeNames);

                if (ep.AllowedRoles is not null)
                    attr.Roles = string.Join(',', ep.AllowedRoles);

                return attr;
            }).ToArray();
    }

    static void AddSecurityPolicy(AuthorizationOptions opts, EndpointDefinition ep)
    {
        if (!ep.RequiresAuthorization())
            return;

        opts.AddPolicy(
            ep.SecurityPolicyName,
            b =>
            {
                b.RequireAuthenticatedUser();

                if (ep.AllowedPermissions?.Count > 0)
                {
                    if (ep.AllowAnyPermission)
                    {
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c => string.Equals(c.Type, Conf.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                     ep.AllowedPermissions.Contains(c.Value, StringComparer.Ordinal)));
                    }
                    else
                    {
                        b.RequireAssertion(
                            x => ep.AllowedPermissions.All(
                                p => x.User.Claims.Any(
                                    c => string.Equals(c.Type, Conf.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(c.Value, p, StringComparison.Ordinal))));
                    }
                }

                if (ep.AllowedClaimTypes?.Count > 0)
                {
                    if (ep.AllowAnyClaim)
                        b.RequireAssertion(x => x.User.Claims.Any(c => ep.AllowedClaimTypes.Contains(c.Type, StringComparer.OrdinalIgnoreCase)));
                    else
                    {
                        b.RequireAssertion(x => ep.AllowedClaimTypes.All(t => x.User.Claims.Any(c => string.Equals(c.Type, t, StringComparison.OrdinalIgnoreCase))));
                    }
                }

                ep.PolicyBuilder?.Invoke(b);

                //note: only claim/permission/policy builder requirements are added here in the security policy
                //      roles and auth schemes are specified in the AuthorizeAttribute in BuildAuthorizeAttributes()
            });
    }
}

sealed class StartupTimer { }

sealed class DuplicateHandlerRegistration { }