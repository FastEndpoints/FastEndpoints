using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FastEndpoints;

/// <summary>
/// provides extensions to easily bootstrap fastendpoints in the asp.net middleware pipeline
/// </summary>
public static class MainExtensions
{
    const string AotWarning = "Reflection-based endpoint discovery is not supported. Use AddFastEndpoints(DiscoveredTypes.All) with the source generator.";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// adds the FastEndpoints services to the ASP.Net middleware pipeline using reflection-based type discovery.
        /// </summary>
        [RequiresUnreferencedCode(AotWarning), RequiresDynamicCode(AotWarning)]
        public IServiceCollection AddFastEndpoints()
            => services.AddFastEndpoints((Action<EndpointDiscoveryOptions>?)null);

        /// <summary>
        /// adds the FastEndpoints services to the ASP.Net middleware pipeline using reflection-based type discovery.
        /// </summary>
        /// <param name="options">optionally specify the reflection-based type discovery options</param>
        [RequiresUnreferencedCode(AotWarning), RequiresDynamicCode(AotWarning)]
        public IServiceCollection AddFastEndpoints(Action<EndpointDiscoveryOptions>? options)
        {
            var opts = new EndpointDiscoveryOptions();
            options?.Invoke(opts);

            var discoveredTypes = EndpointData.DiscoverTypes(opts);
            var cmdHandlerRegistry = new CommandHandlerRegistry();
            MessagingExtensions.RegisterHandlers(discoveredTypes, cmdHandlerRegistry);
            var endpointData = new EndpointData(discoveredTypes);

            return AddFastEndpointsCore(services, cmdHandlerRegistry, endpointData);
        }

        /// <summary>
        /// adds the FastEndpoints services to the ASP.Net middleware pipeline using source-generated discovered types.
        /// pass one <see cref="List{Type}" /> per referenced assembly, e.g.:
        /// <c>AddFastEndpoints(Lib1.DiscoveredTypes.All, Lib2.DiscoveredTypes.All)</c>
        /// </summary>
        /// <param name="discoveredTypes">one or more lists of source-generated discovered types, one per referenced assembly</param>
        public IServiceCollection AddFastEndpoints(params List<Type>[] discoveredTypes)
        {
            var allTypes = discoveredTypes.SelectMany(t => t).ToList();
            var cmdHandlerRegistry = new CommandHandlerRegistry();
            MessagingExtensions.RegisterHandlers(allTypes, cmdHandlerRegistry);
            var endpointData = new EndpointData(allTypes);

            return AddFastEndpointsCore(services, cmdHandlerRegistry, endpointData);
        }
    }

    static IServiceCollection AddFastEndpointsCore(IServiceCollection services, CommandHandlerRegistry cmdHandlerRegistry, EndpointData endpointData)
    {
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

    static readonly Lock _serializerConfigLock = new();
    internal static volatile bool SerializerConfigured;

    [UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
    public static IEndpointRouteBuilder MapFastEndpoints(this IEndpointRouteBuilder app, Action<Cfg>? configAction = null)
    {
        ServiceResolver.Instance = app.ServiceProvider.GetRequiredService<IServiceResolver>();
        ConfigureSerializerOnce(app, configAction);
        Cfg.BndOpts.AddTypedHeaderValueParsers();

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

        EndpointRouteMapper.Map(app);

        return app;
    }

    static void ConfigureSerializerOnce(IEndpointRouteBuilder app, Action<Cfg>? configAction)
    {
        lock (_serializerConfigLock)
        {
            if (SerializerConfigured)
                return;

            var serializerOptions = app.ServiceProvider.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions;

            if (serializerOptions is not null)
                Cfg.SerOpts.Options = new(serializerOptions);

            Cfg.SerOpts.Options.ConfigureSerializer(app.ServiceProvider.GetRequiredService<Cfg>(), configAction);

            SerializerConfigured = true;
        }
    }

    // Kept on MainExtensions so OpenApi/Agents friend assemblies retain a stable CLR declaring type
    // for this internal extension method (independent Agents package versions).
    internal static string BuildRoute(this StringBuilder builder, int epVersion, string route, string? prefixOverride)
    {
        var prefix = RoutePrefixHelper.Resolve(Cfg.EpOpts.RoutePrefix, prefixOverride);

        if (prefix is not null)
        {
            builder.Append('/')
                   .Append(prefix)
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
            // {rPrfx}/{p}{ver}/{route}
            // mobile/v1/customer/retrieve

            if (Cfg.VerOpts.PrependToRoute is true)
                AppendVersion(builder, epVersion, trailingSlash: true);

            if (builder.Length > 0 && route.StartsWith('/'))
                builder.Length--;

            builder.Append(route);

            // {rPrfx}/{route}/{p}{ver}
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
}

sealed class StartupTimer;

sealed class DuplicateHandlerRegistration;

[JsonSerializable(typeof(string)), JsonSerializable(typeof(IEnumerable<string>)), JsonSerializable(typeof(ErrorResponse)), JsonSerializable(typeof(ProblemDetails))]
sealed partial class FastEndpointsSerializerContext : JsonSerializerContext;