using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class X402RegistrationExtensions
{
    /// <summary>
    /// adds x402 payment services
    /// </summary>
    public static IServiceCollection AddX402(this IServiceCollection services)
    {
        services.AddHttpClient<IX402FacilitatorClient, X402FacilitatorClient>(
            c =>
            {
                if (!string.IsNullOrWhiteSpace(Cfg.X402Opts.FacilitatorUrl))
                    c.BaseAddress = new(Cfg.X402Opts.FacilitatorUrl, UriKind.Absolute);

                c.Timeout = Cfg.X402Opts.Timeout;
            });

        return services;
    }

    /// <summary>
    /// adds x402 payment middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseX402(this IApplicationBuilder app, Action<X402Options>? configure = null)
    {
        configure?.Invoke(Cfg.X402Opts);
        Cfg.X402Opts.Enabled = true;
        Cfg.X402Opts.ThrowIfInvalid();

        return app.UseMiddleware<X402Middleware>();
    }
}