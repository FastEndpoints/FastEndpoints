using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FastEndpoints;

static class EndpointRouteMapper
{
    [UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
    internal static void Map(IEndpointRouteBuilder app)
    {
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

            if (Cfg.EpOpts.WarmupRequested && (Cfg.EpOpts.WarmupFilter is null || Cfg.EpOpts.WarmupFilter(def)))
                EndpointWarmup.WarmupEndpoint(def, scope.ServiceProvider);

            EndpointSecurityPolicies.AddSecurityPolicy(authOptions, def);

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

                    hb.AddSwaggerDefaults(def, finalRoute); //always do this first here

                    if (def.AnonymousVerbs?.Contains(verb) is true)
                        hb.AllowAnonymous();
                    else
                        hb.RequireAuthorization(EndpointSecurityPolicies.BuildAuthorizeAttributes(def));

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
            def.AttribsToForward = null;
            def.IsLocked = true;
        }

        if (Cfg.EpOpts.WarmupRequested)
            MessagingExtensions.WarmupMessaging(app.ServiceProvider);

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

        CommandExtensions.TestCommandHandlerMarker ??= Types.TestCommandHandlerMarker;

        app.MapGet(
               "_test_url_cache_",
               () => Results.Json(IEndpoint.GetTestUrlCache(), Cfg.SerOpts.Options))
           .ExcludeFromDescription();
    }
}
