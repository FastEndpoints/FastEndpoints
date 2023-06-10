using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace FastEndpoints;

/// <summary>
/// represents a connection to a remote server that hosts command handlers (<see cref="ICommandHandler{TCommand, TResult}"/>)
/// <para>call <see cref="Register{TCommand, TResult}"/> method to map the commands</para>
/// </summary>
public sealed class RemoteConnection
{
    private GrpcChannel? _channel;
    private readonly Dictionary<Type, ICommandExecutor> _executorMap = new(); //key: tCommand, val: command executor wrapper

    /// <summary>
    /// grpc channel settings
    /// </summary>
    public GrpcChannelOptions ChannelOptions { get; set; } = new()
    {
        HttpHandler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
            EnableMultipleHttp2Connections = true
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
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            }}
        },
        MaxRetryAttempts = 5
    };

    /// <summary>
    /// the address of the remote server
    /// </summary>
    public string RemoteAddress { get; init; }

    internal RemoteConnection(string address)
    {
        RemoteAddress = address;
    }

    /// <summary>
    /// register a "unary" command (<see cref="ICommand{TResult}"/>) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public void Register<TCommand, TResult>() where TCommand : class, ICommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        RemoteConnectionExtensions.CommandToRemoteMap[tCommand] = this;
        _channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        _executorMap[tCommand] = new UnaryCommandExecutor<TCommand, TResult>(_channel);
    }

    internal Task<TResult> ExecuteUnary<TResult>(ICommand<TResult> cmd, Type tCommand, CallOptions opts) where TResult : class
    {
        if (!_executorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((IUnaryCommandExecutor<TResult>)executor).ExecuteUnary(cmd, opts);
    }

    /// <summary>
    /// register a "server stream" command (<see cref="IServerStreamCommand{TResult}"/>) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result stream</typeparam>
    public void RegisterServerStream<TCommand, TResult>() where TCommand : class, IServerStreamCommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        RemoteConnectionExtensions.CommandToRemoteMap[tCommand] = this;
        _channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        _executorMap[tCommand] = new ServerStreamCommandExecutor<TCommand, TResult>(_channel);
    }

    internal IAsyncStreamReader<TResult> ExecuteServerStream<TResult>(IServerStreamCommand<TResult> cmd, Type tCommand, CallOptions opts) where TResult : class
    {
        if (!_executorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((IServerStreamCommandExecutor<TResult>)executor).ExecuteServerStream(cmd, opts);
    }

    /// <summary>
    /// register a remote handler for a "client stream" (<see cref="IAsyncEnumerable{T}"/>) for this remote connection.
    /// </summary>
    /// <typeparam name="T">the type of the items in the stream</typeparam>
    /// <typeparam name="TResult">the type of the result that will be received when the stream ends</typeparam>
    public void RegisterClientStream<T, TResult>() where T : class where TResult : class
    {
        var tCommand = typeof(IAsyncEnumerable<T>);
        RemoteConnectionExtensions.CommandToRemoteMap[tCommand] = this;
        _channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        _executorMap[tCommand] = new ClientStreamCommandExecutor<T, TResult>(_channel);
    }

    internal Task<TResult> ExecuteClientStream<T, TResult>(IAsyncEnumerable<T> cmd, Type tCommand, CallOptions opts) where T : class where TResult : class
    {
        if (!_executorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((IClientStreamCommandExecutor<T, TResult>)executor).ExecuteClientStream(cmd, opts);
    }
}