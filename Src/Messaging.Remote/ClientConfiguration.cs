using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace FastEndpoints;

public sealed class ClientConfiguration
{
    private GrpcChannel? _channel;
    private readonly Dictionary<Type, ICommandExecutor> _commandExecutorMap = new(); //key: tCommand, val: command executor wrapper

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

        _commandExecutorMap[tCommand] = new CommandExecutor<TCommand, TResult>(_channel);
    }

    internal Task<TResult> Execute<TResult>(ICommand<TResult> cmd, Type tCommand, CancellationToken ct) where TResult : class
    {
        if (!_commandExecutorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((ICommandExecutor<TResult>)executor).Execute(cmd, ct);
    }
}

internal interface ICommandExecutor { }

internal interface ICommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> Execute(ICommand<TResult> command, CancellationToken ct);
}

internal sealed class CommandExecutor<TCommand, TResult> : ICommandExecutor<TResult>
    where TCommand : class, ICommand<TResult>
    where TResult : class
{
    private readonly Method<TCommand, TResult> _method;
    private readonly CallInvoker _invoker;

    public CommandExecutor(GrpcChannel channel)
    {
        _invoker = channel.CreateCallInvoker();
        _method = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: nameof(ICommandHandler<TCommand, TResult>.ExecuteAsync),
            requestMarshaller: new MsgPackMarshaller<TCommand>(),
            responseMarshaller: new MsgPackMarshaller<TResult>());
    }

    public Task<TResult> Execute(ICommand<TResult> cmd, CancellationToken ct)
    {
        var call = _invoker.AsyncUnaryCall(_method, null, new CallOptions(cancellationToken: ct), (TCommand)cmd);
        return call.ResponseAsync;
    }
}