using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace FastEndpoints;

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
    public string Address { get; init; }

    private readonly IServiceProvider _provider;
    private GrpcChannel? _channel;
    private readonly Dictionary<Type, IMethod> _methodMap = new(); //key: tCommand, val: method

    public RemoteServerConfiguration(string address, IServiceProvider provider)
    {
        Address = address;
        _provider = provider;
    }

    public void Register<TCommand, TResult>()
        where TCommand : class, ICommand<TResult>
        where TResult : class
    {
        var tCommand = typeof(TCommand);
        var remoteMap = MessagingClientExtensions.CommandsToRemotesMap;

        remoteMap.TryGetValue(tCommand, out var servers);
        if (servers is null)
        {
            servers = new();
            remoteMap[tCommand] = servers;
        }

        if (!servers.Any(s => s.Address == Address))
        {
            servers.Add(this);
        }
        else
        {
            return;
        }

        _channel ??= GrpcChannel.ForAddress(Address, ChannelOptions);

        _methodMap[tCommand] = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: nameof(ICommandHandler<TCommand, TResult>.ExecuteAsync),
            requestMarshaller: new MsgPackMarshaller<TCommand>(),
            responseMarshaller: new MsgPackMarshaller<TResult>());
    }
}