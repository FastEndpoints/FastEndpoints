using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FastEndpoints;

/// <summary>
/// provides extensions to easily bootstrap fastendpoints in the asp.net middleware pipeline
/// </summary>
[UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
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
        services.AddSingleton<Cfg>();

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
    public static IApplicationBuilder UseFastEndpoints(this IApplicationBuilder app, Action<Cfg>? configAction = null)
    {
        if (app is not IEndpointRouteBuilder routeBuilder)
            throw new InvalidCastException($"Cannot cast [{nameof(app)}] to IEndpointRouteBuilder");

        routeBuilder.MapFastEndpoints(configAction);

        return app;
    }

    public static IEndpointRouteBuilder MapFastEndpoints(this IEndpointRouteBuilder app, Action<Cfg>? configAction = null)
    {
        ServiceResolver.Instance = app.ServiceProvider.GetRequiredService<IServiceResolver>();
        var jsonOpts = app.ServiceProvider.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions;
        Cfg.SerOpts.AspNetCoreOptions = jsonOpts; // store reference to original for IResult types
        Cfg.SerOpts.Options = jsonOpts is not null
                                  ? new(jsonOpts) //make a copy to avoid configAction modifying the global JsonOptions
                                  : Cfg.SerOpts.Options;
        configAction?.Invoke(app.ServiceProvider.GetRequiredService<Cfg>());

        if (Cfg.ValOpts.UsePropertyNamingPolicy && Cfg.SerOpts.Options.PropertyNamingPolicy is not null)
        {
            ValidatorOptions.Global.PropertyNameResolver =
                (_, memberInfo, expression) =>
                {
                    if (memberInfo is null)
                        return null;

                    if (expression is null)
                        return Cfg.SerOpts.Options.PropertyNamingPolicy.ConvertName(memberInfo.Name);

                    var chain = PropertyChain.FromExpression(expression);

                    return Cfg.SerOpts.Options.PropertyNamingPolicy.ConvertName(chain.Count > 0 ? chain.ToString() : memberInfo.Name);
                };
        }

        Cfg.SerOpts.Options.ConfigureSerializer();
        Cfg.BndOpts.AddTypedHeaderValueParsers(Cfg.SerOpts.Options);

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

            if (Cfg.EpOpts.Filter is not null && !Cfg.EpOpts.Filter(def))
                continue;

            if (def.Verbs.Length == 0)
                throw new ArgumentException($"No HTTP Verbs declared on: [{def.EndpointType.FullName}]");
            if (def.Routes.Length == 0)
                throw new ArgumentException($"No Routes declared on: [{def.EndpointType.FullName}]");

            Cfg.EpOpts.Configurator?.Invoke(def); //apply global ep settings to the definition

            if (def.AntiforgeryEnabled && (app.ServiceProvider.GetService<IAntiforgery>() is null || AntiforgeryMiddleware.IsRegistered is false))
                throw new InvalidOperationException("AntiForgery middleware setup is incorrect!");

            AddSecurityPolicy(authOptions, def);

            var routeNum = 0;

            foreach (var route in def.Routes)
            {
                var finalRoute = routeBuilder.BuildRoute(def.Version.Current, route, def.OverriddenRoutePrefix);
                IEndpoint.SetTestUrl(def.EndpointType, finalRoute);

                routeNum++;

                foreach (var verb in def.Verbs)
                {
                    var hb = app.MapMethods(finalRoute, [verb], () => FeRequestHandler.Instance);

                    hb.WithName(
                        Cfg.EpOpts.NameGenerator(
                            new(
                                def.EndpointType,
                                def.Verbs.Length > 1 ? verb : null,
                                def.Routes.Length > 1 ? routeNum : null,
                                def.EndpointTags?.Count > 0 ? def.EndpointTags[0] : null))); //user can override this via Options(x=>x.WithName(...))

                    hb.WithMetadata(def.EndpointMetadata is not null ? [def, ..def.EndpointMetadata] : [def]);

                    if (def.AttribsToForward is not null)
                        hb.WithMetadata(def.AttribsToForward.ToArray());

                    hb.AddSwaggerDefaults(def); //always do this first here

                    if (def.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(BuildAuthorizeAttributes(def));

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
                    def.InitAcceptsMetaData(hb);      //must come after UserConfigAction

                    var key = $"{verb}:{finalRoute}";
                    routeToHandlerCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
                    totalEndpointCount++;
                }
                def.AttribsToForward = null;
                def.IsLocked = true;
            }
        }

        app.ServiceProvider.GetRequiredService<ILogger<StartupTimer>>().EndpointsRegistered(totalEndpointCount, endpoints.Stopwatch.ElapsedMilliseconds.ToString("N0"));

        endpoints.Stopwatch.Stop();

        if (!Cfg.VerOpts.IsUsingAspVersioning)
        {
            var duplicatesDetected = false;
            var logger = app.ServiceProvider.GetRequiredService<ILogger<DuplicateHandlerRegistration>>();

            foreach (var kvp in routeToHandlerCounts)
            {
                if (kvp.Value <= 1)
                    continue;

                duplicatesDetected = true;
                logger.MultipleEndpointsRegisteredForRoute(kvp.Key, kvp.Value);
            }

            if (duplicatesDetected)
                throw new InvalidOperationException("Duplicate routes detected! See log for more details.");
        }

        CommandExtensions.TestHandlersPresent = app.ServiceProvider.GetService<TestCommandHandlerMarker>() is not null;

        app.MapGet("_test_url_cache_", () => TypedResults.Ok(IEndpoint.GetTestUrlCache()))
           .ExcludeFromDescription();

        return app;
    }

    internal static string BuildRoute(this StringBuilder builder, int epVersion, string route, string? prefixOverride)
    {
        if (Cfg.EpOpts.RoutePrefix is not null && prefixOverride != string.Empty)
        {
            builder.Append('/')
                   .Append(prefixOverride ?? Cfg.EpOpts.RoutePrefix)
                   .Append('/');
        }

        if (Cfg.VerOpts.RouteTemplate is not null && (epVersion > 0 || Cfg.VerOpts.DefaultVersion != 0))
        {
            var index = route.IndexOf(Cfg.VerOpts.RouteTemplate, StringComparison.Ordinal);

            if (index < 0)
                throw new InvalidOperationException($"The route [{route}], doesn't contain the versioning template pattern [{Cfg.VerOpts.RouteTemplate}]!");

            SetVersion(builder, Cfg.VerOpts.RouteTemplate, index, route, epVersion);
        }
        else
        {
            // {rPrfix}/{p}{ver}/{route}
            // mobile/v1/customer/retrieve

            if (Cfg.VerOpts.PrependToRoute is true)
                AppendVersion(builder, epVersion, trailingSlash: true);

            if (builder.Length > 0 && route.StartsWith('/'))
                builder.Length--;

            builder.Append(route);

            // {rPrfix}/{route}/{p}{ver}
            // mobile/customer/retrieve/v1

            if (Cfg.VerOpts.PrependToRoute is not true)
                AppendVersion(builder, epVersion, trailingSlash: false);
        }

        var final = builder.ToString();
        builder.Clear();

        return final;

        static void SetVersion(StringBuilder builder, string routeTemplate, int indexPos, string route, int epVersion)
        {
            if (builder.Length > 0 && builder[^1] == '/' && route.StartsWith('/'))
                builder.Length--;

            builder.Append(route.AsSpan(0, indexPos))                              //add up to beginning of routeTemplate
                   .Append(Cfg.VerOpts.Prefix ?? "v")                              //add version prefix
                   .Append(epVersion > 0 ? epVersion : Cfg.VerOpts.DefaultVersion) //add version number
                   .Append(route.AsSpan(indexPos + routeTemplate.Length));         //add the part after routeTemplate
        }

        static void AppendVersion(StringBuilder builder, int epVersion, bool trailingSlash)
        {
            var prefix = Cfg.VerOpts.Prefix ?? "v";
            var version = epVersion > 0
                              ? epVersion
                              : Cfg.VerOpts.DefaultVersion;

            if (version == 0)
                return;

            if (builder.Length > 0 && builder[^1] != '/')
                builder.Append('/');

            builder.Append(prefix)
                   .Append(version);

            if (trailingSlash)
                builder.Append('/');
        }
    }

    static IAuthorizeData[] BuildAuthorizeAttributes(EndpointDefinition ep)
    {
        var policiesToAdd = new List<string>();

        if (ep.PreBuiltUserPolicies?.Count > 0)
            policiesToAdd.AddRange(ep.PreBuiltUserPolicies);

        if (ep.RequiresAuthorization())
            policiesToAdd.Add(ep.SecurityPolicyName);

        // ReSharper disable once CoVariantArrayConversion
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
                                c => string.Equals(c.Type, Cfg.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                     ep.AllowedPermissions.Contains(c.Value, StringComparer.Ordinal)));
                    }
                    else
                    {
                        b.RequireAssertion(
                            x => ep.AllowedPermissions.All(
                                p => x.User.Claims.Any(
                                    c => string.Equals(c.Type, Cfg.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(c.Value, p, StringComparison.Ordinal))));
                    }
                }

                if (ep.AllowedScopes?.Count > 0)
                {
                    if (ep.AllowAnyScope)
                    {
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c => string.Equals(c.Type, Cfg.SecOpts.ScopeClaimType, StringComparison.OrdinalIgnoreCase) &&
                                     Cfg.SecOpts.ScopeParser(c.Value).Any(s => ep.AllowedScopes.Contains(s, StringComparer.OrdinalIgnoreCase))));
                    }
                    else
                    {
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c =>
                                {
                                    var incomingScopes = Cfg.SecOpts.ScopeParser(c.Value); //run parser func only once!

                                    return string.Equals(c.Type, Cfg.SecOpts.ScopeClaimType, StringComparison.OrdinalIgnoreCase) &&
                                           ep.AllowedScopes.All(s => incomingScopes.Contains(s, StringComparer.OrdinalIgnoreCase));
                                }));
                    }
                }

                if (ep.AllowedClaimTypes?.Count > 0)
                {
                    if (ep.AllowAnyClaim)
                        b.RequireAssertion(x => x.User.Claims.Any(c => ep.AllowedClaimTypes.Contains(c.Type, StringComparer.OrdinalIgnoreCase)));
                    else
                        b.RequireAssertion(x => ep.AllowedClaimTypes.All(t => x.User.Claims.Any(c => string.Equals(c.Type, t, StringComparison.OrdinalIgnoreCase))));
                }

                ep.PolicyBuilder?.Invoke(b);

                //note: only claim/permission/scope/policy-builder requirements are added here in the security policy
                //      roles and auth schemes are specified in the AuthorizeAttribute in BuildAuthorizeAttributes()
            });
    }

    extension(RouteHandlerBuilder b)
    {
        void AddSwaggerDefaults(EndpointDefinition ep)
        {
            //clearing all produces metadata before proceeding - https://github.com/FastEndpoints/FastEndpoints/issues/833
            //this is possibly related to .net 9+ only, but we'll be covering all bases this way.
            b.Add(
                eb =>
                {
                    for (var i = eb.Metadata.Count - 1; i >= 0; i--)
                    {
                        if (eb.Metadata[i] is IProducesResponseTypeMetadata)
                            eb.Metadata.RemoveAt(i);
                    }
                });

            var isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(ep.ReqDtoType);

            if (isPlainTextRequest)
            {
                b.Accepts(ep.ReqDtoType, "text/plain", "application/json");
                b.ProducesDeDuped(200, ep.ResDtoType, ["text/plain", "application/json"]);

                return;
            }

            if (ep.ReqDtoType != Types.EmptyRequest)
            {
                if (ep.ReqDtoType.AllPropsAreNonJsonSourced())
                    b.Accepts(ep.ReqDtoType, "*/*");
                else if (ep.Verbs.Any(m => m is "GET" or "HEAD" or "DELETE"))
                    b.Accepts(ep.ReqDtoType, "*/*", "application/json");
                else
                    b.Accepts(ep.ReqDtoType, "application/json");
            }

            if (ep.ExecuteAsyncReturnsIResult)
                b.Add(eb => ProducesMetaForResultOfResponse.AddMetadata(eb, ep.ResDtoType));
            else
            {
                if (ep.ResDtoType == Types.Object || ep.ResDtoType == Types.EmptyResponse)
                    b.ProducesDeDuped(204, Types.Void, []);
                else
                    b.ProducesDeDuped(200, ep.ResDtoType, ["application/json"]);
            }

            if (ep.AnonymousVerbs?.Length is null or 0)
                b.ProducesDeDuped(401, Types.Void, []);

            if (ep.RequiresAuthorization())
                b.ProducesDeDuped(403, Types.Void, []);

            if (Cfg.ErrOpts.ProducesMetadataType is not null && ep.ValidatorType is not null)
                b.ProducesDeDuped(Cfg.ErrOpts.StatusCode, Cfg.ErrOpts.ProducesMetadataType, [Cfg.ErrOpts.ContentType]);
        }

        void ProducesDeDuped(int statusCode, Type type, string[] contentTypes)
        {
            b.Finally(
                b1 =>
                {
                    for (var i = 0; i < b1.Metadata.Count; i++)
                    {
                        int? code = b1.Metadata[i] switch
                        {
                            IProducesResponseTypeMetadata p => p.StatusCode,
                            IApiResponseMetadataProvider a => a.StatusCode,
                            _ => null
                        };

                        if (code is null)
                            continue;

                        switch (statusCode)
                        {
                            case >= 200 and < 300 when code is >= 200 and < 300:
                            case >= 400 and < 500 when code == statusCode:
                                return;
                        }
                    }

                    b1.Metadata.Add(new DefaultProducesResponseMetadata(type, statusCode, contentTypes));
                });
        }
    }

    static bool AllPropsAreNonJsonSourced(this Type tRequest)
        => tRequest.BindableProps().All(p => p.CustomAttributes.Any(a => Types.NonJsonBindingAttribute.IsAssignableFrom(a.AttributeType)));
}

sealed class StartupTimer;

sealed class DuplicateHandlerRegistration;

[JsonSerializable(typeof(string)), JsonSerializable(typeof(IEnumerable<string>)), JsonSerializable(typeof(ErrorResponse)), JsonSerializable(typeof(ProblemDetails))]
sealed partial class FastEndpointsSerializerContext : JsonSerializerContext;