using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
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

public static class MessagingClientExtensions
{
    //key: tCommand
    //val: list of remote servers that has handlers listening
    private static readonly Dictionary<Type, List<RemoteServerConfiguration>> _remotes = new();

    public static IHost MapRemoteHandlers(this IHost _, string serverAddress, Action<RemoteServerConfiguration> r)
    {

    }
}

public sealed class RemoteServerConfiguration
{
    public GrpcChannelOptions ChannelOptions { get; set; } = new()
    {
        HttpHandler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
            EnableMultipleHttp2Connections = true,
            SslOptions = new() { RemoteCertificateValidationCallback = (_, __, ___, ____) => true }
        },
        ServiceConfig = new()
        {
            MethodConfigs = { new()
            {
                Names = { MethodName.Default },
                RetryPolicy = new()
                {
                    MaxAttempts = 5, // must be <= MaxRetryAttempts
                    InitialBackoff = TimeSpan.FromSeconds(1),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.Unknown }
                }
            }}
        },
        MaxRetryAttempts = 5
    };

    private string _address;
    private readonly GrpcChannel _channel;

    public RemoteServerConfiguration(string address)
    {
        _address = address;
    }


}