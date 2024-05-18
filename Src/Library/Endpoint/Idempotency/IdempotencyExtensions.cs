#if NET7_0_OR_GREATER
    using Microsoft.Extensions.DependencyInjection;

    namespace FastEndpoints;

    public static class IdempotencyExtensions
    {
        /// <summary>
        /// enable idempotency features
        /// </summary>
        /// <param name="cfg">global configuration settings for idempotency middleware</param>
        public static IServiceCollection AddIdempotency(this IServiceCollection services, Action<IdempotencyConfig>? cfg = null)
        {
            services.AddOutputCache(
                c =>
                {
                    var conf = new IdempotencyConfig();
                    cfg?.Invoke(conf);
                    c.SizeLimit = conf.InMemoryCacheSize;
                    c.MaximumBodySize = conf.MaxResponseBodySize;
                    c.UseCaseSensitivePaths = conf.UseCaseSensitivePaths;
                    c.AddBasePolicy(new IdempotencyPolicy());
                });

            return services;
        }
    }
#endif