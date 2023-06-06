using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace FastEndpoints;

public sealed class ClientConfiguration
{
    private GrpcChannel? _channel;
    private readonly Dictionary<Type, IMethod> _methodMap = new(); //key: tCommand, val: method

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

    public ClientConfiguration(string address)
    {
        Address = address;
    }

    public void Register<TCommand, TResult>() where TCommand : class, ICommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        var remoteMap = ClientExtensions.CommandToClientMap;

        remoteMap.TryGetValue(tCommand, out var server);

        if (server is null)
            remoteMap[tCommand] = this;
        else
            return;

        _channel ??= GrpcChannel.ForAddress(Address, ChannelOptions);

        _methodMap[tCommand] = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: nameof(ICommandHandler<TCommand, TResult>.ExecuteAsync),
            requestMarshaller: new MsgPackMarshaller<TCommand>(),
            responseMarshaller: new MsgPackMarshaller<TResult>());
    }

    internal Task<TResult> Execute<TCommand, TResult>(TCommand cmd, Type tCommand, CancellationToken ct)
        where TCommand : class, ICommand<TResult>
        where TResult : class
    {
        var invoker = _channel!.CreateCallInvoker();
        var method = (Method<TCommand, TResult>)_methodMap[tCommand];
        var call = invoker.AsyncUnaryCall(method, null, new CallOptions(cancellationToken: ct), cmd);
        return call.ResponseAsync;
    }
}