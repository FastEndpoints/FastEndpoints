using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// DI and pipeline registration for financial-mode HTTP idempotency
/// </summary>
public static class FinancialIdempotencyExtensions
{
    internal const string RegistrationKey = "FastEndpoints.FinancialIdempotency";

    /// <summary>
    /// Assert that this rejected request performed no business side effects. Only honored on a normally completed non-2xx response. Never use after an uncertain operation.
    /// </summary>
    public static void RejectFinancialIdempotencyWithoutSideEffects(this HttpContext context)
    {
        if (!context.Items.ContainsKey(FinancialIdempotencyMiddleware.RejectionKey))
            throw new InvalidOperationException("No active financial reservation!");

        context.Items[FinancialIdempotencyMiddleware.RejectionKey] = true;
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// register financial idempotency with the built-in in-memory store.
        /// fingerprint <see cref="IdempotencyExtensions.AddIdempotency" /> is unchanged and separate.
        /// </summary>
        public IServiceCollection AddFinancialIdempotency(Action<FinancialIdempotencyConfig>? cfg = null)
        {
            var conf = RegisterConfig(services, cfg);
            services.AddSingleton<IFinancialIdempotencyStore>(
                sp => new MemoryFinancialIdempotencyStore(
                    conf.InMemoryStoreSize,
                    sp.GetService<ILogger<MemoryFinancialIdempotencyStore>>(),
                    maxEntries: conf.InMemoryMaxEntries));

            return services;
        }

        /// <summary>
        /// register financial idempotency with a custom store (Redis/SQL/NATS). the store is a singleton.
        /// the implementor owns distributed atomicity of <see cref="IFinancialIdempotencyStore.TryBeginAsync" />.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2091")]
        public IServiceCollection AddFinancialIdempotency<TStore>(Action<FinancialIdempotencyConfig>? cfg = null)
            where TStore : class, IFinancialIdempotencyStore
        {
            RegisterConfig(services, cfg);
            services.AddSingleton<IFinancialIdempotencyStore, TStore>();

            return services;
        }

        /// <summary>
        /// register financial idempotency with an existing store instance (tests)
        /// </summary>
        public IServiceCollection AddFinancialIdempotency(IFinancialIdempotencyStore store, Action<FinancialIdempotencyConfig>? cfg = null)
        {
            RegisterConfig(services, cfg);
            services.AddSingleton(store);

            return services;
        }
    }

    /// <summary>
    /// insert financial idempotency middleware.
    /// place it after authentication/authorization and next to <c>UseOutputCache</c>, before <c>UseFastEndpoints</c>/<c>MapFastEndpoints</c>.
    /// no-ops for endpoints that did not call <c>FinancialIdempotency()</c>.
    /// </summary>
    public static IApplicationBuilder UseFinancialIdempotency(this IApplicationBuilder app)
    {
        if (app.Properties.ContainsKey(RegistrationKey))
            throw new InvalidOperationException("Financial idempotency middleware is already registered in this pipeline!");

        app.Properties[RegistrationKey] = true;
        app.UseMiddleware<FinancialIdempotencyMiddleware>();

        return app;
    }

    static FinancialIdempotencyConfig RegisterConfig(IServiceCollection services, Action<FinancialIdempotencyConfig>? cfg)
    {
        if (services.Any(d => d.ServiceType == typeof(FinancialIdempotencyConfig)))
            throw new InvalidOperationException("Financial idempotency is already registered!");

        var conf = new FinancialIdempotencyConfig();
        cfg?.Invoke(conf);

        if (conf.InMemoryStoreSize <= 0 ||
            conf.InMemoryMaxEntries <= 0 ||
            !FinancialIdempotencyOptions.IsValidDuration(conf.DefaultDuration))
            throw new InvalidOperationException("Invalid financial idempotency global limits!");

        services.AddSingleton(conf);

        return conf;
    }
}