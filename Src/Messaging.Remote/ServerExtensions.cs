using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

public static class MessagingServerExtensions
{
    public static IServiceCollection AddMessagingServer(this IServiceCollection services, Action<ServerConfiguration> s)
    {
        var server = new ServerConfiguration(services);
        s(server);

        services.TryAddSingleton(server);

        return services;
    }

    public static IHost StartMessagingServer(this IHost host)
    {
        var server = host.Services.GetRequiredService<ServerConfiguration>();
        server.StartServer(host.Services);
        return host;
    }
}