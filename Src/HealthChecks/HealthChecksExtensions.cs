using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FastEndpoints;

/// <summary>
/// Extension methods for adding health check endpoints to the service collection.
/// </summary>
public static class HealthChecksExtensions
{
    /// <summary>
    /// Adds liveness and readiness health check endpoints to the application.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This method registers two health check endpoints:
    ///     <list type="bullet">
    ///         <item>
    ///             <term>/health/live (liveness)</term>
    ///             <description>Indicates if the process is running. Does not check dependencies - returns 200 OK if the process is alive.</description>
    ///         </item>
    ///         <item>
    ///             <term>/health/ready (readiness)</term>
    ///             <description>Indicates if the application is ready to receive traffic. Runs all registered health checks.</description>
    ///         </item>
    ///     </list>
    ///     </para>
    ///     <para>
    ///     A "self" health check is automatically registered to ensure at least one check exists.
    ///     </para>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="ServiceHealthChecksOptions" />.</param>
    /// <param name="configureChecks">An optional action to configure additional health checks via <see cref="IHealthChecksBuilder" />.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
    /// <example>
    ///     <code>
    /// // Basic usage with defaults
    /// builder.Services.AddServiceHealthChecks();
    ///
    /// // Custom paths
    /// builder.Services.AddServiceHealthChecks(
    ///     configureOptions: opts =>
    ///     {
    ///         opts.LivePath = "/alive";
    ///         opts.ReadyPath = "/ready";
    ///     });
    ///
    /// // Add custom health checks
    /// builder.Services.AddServiceHealthChecks(
    ///     configureChecks: hc =>
    ///     {
    ///         hc.AddNpgSql(connectionString, name: "postgres");
    ///         hc.AddRedis("localhost:6379", name: "redis");
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services,
                                                            Action<ServiceHealthChecksOptions>? configureOptions = null,
                                                            Action<IHealthChecksBuilder>? configureChecks = null)
    {
        if (configureOptions is not null)
            services.Configure(configureOptions);

        // Even if the user hasn't added any checks,
        // the readiness endpoint will still work (it will just be Healthy).
        var hcBuilder = services.AddHealthChecks();

        // Base self-check (optional, but useful to have at least something)
        hcBuilder.AddCheck("self", () => HealthCheckResult.Healthy());

        configureChecks?.Invoke(hcBuilder);

        // Register endpoints via StartupFilter
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ServiceHealthChecksStartupFilter>());

        return services;
    }
}