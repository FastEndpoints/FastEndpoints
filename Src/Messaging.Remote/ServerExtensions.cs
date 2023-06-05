using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

public static class MessagingServerExtensions
{
    public static IServiceCollection AddMessagingServer(this IServiceCollection services, Action<ServerConfiguration> s)
    {
        var opts = new ServerConfiguration(services);
        s(opts);

        services.TryAddSingleton(opts);

        return services;
    }

    public static IHost StartMessagingServer(this IHost host)
    {
        var config = host.Services.GetRequiredService<ServerConfiguration>();
        config.SetServiceProvider(host.Services);
        config.StartServer();
        return host;
    }
}