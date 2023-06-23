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
    //key: tCommand or tEventHandler
    //val: remote server that hosts command handlers/event buses
    internal static Dictionary<Type, RemoteConnection> RemoteMap { get; } = new();

    private GrpcChannel? _channel;
    private readonly Dictionary<Type, ICommandExecutor> _executorMap = new(); //key: tCommand, val: command executor wrapper
    private readonly IServiceProvider? _serviceProvider;

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

    internal RemoteConnection(string address, IServiceProvider? serviceProvider = null)
    {
        RemoteAddress = address;
        _serviceProvider = serviceProvider; //only null in unit tests for commands (non-events).
    }

    /// <summary>
    /// subscribe to a broadcast channel for a given event type (<typeparamref name="TEvent"/>) on the remote host.
    /// the received events will be handled by the specified handler (<typeparamref name="TEventHandler"/>) on this machine.
    /// </summary>
    /// <typeparam name="TEvent">the type of the events that will be received</typeparam>
    /// <typeparam name="TEventHandler">the handler that will be handling the received events</typeparam>
    /// <param name="callOptions">the call options</param>
    public void Subscribe<TEvent, TEventHandler>(CallOptions callOptions = default) where TEvent : class, IEvent where TEventHandler : IEventHandler<TEvent>
    {
        var tEventHandler = typeof(TEventHandler);
        RemoteMap[tEventHandler] = this;
        _channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);

        if (!_executorMap.ContainsKey(tEventHandler))
        {
            if (!EventSubscriberStorage.IsInitalized)
                EventSubscriberStorage.Initialize<InMemoryEventStorageRecord, InMemoryEventSubscriberStorage>(_serviceProvider!);

            var eventExecutor = new EventHandlerExecutor<TEvent, TEventHandler>(_channel, _serviceProvider);
            _executorMap[tEventHandler] = eventExecutor;
            eventExecutor.Start(callOptions);
        }
    }

    /// <summary>
    /// register a "void" command (<see cref="ICommand"/>) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    public void Register<TCommand>() where TCommand : class, ICommand
    {
        var tCommand = typeof(TCommand);
        RemoteMap[tCommand] = this;
        _channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        _executorMap[tCommand] = new VoidCommandExecutor<TCommand>(_channel);
    }

    internal Task ExecuteVoid(ICommand cmd, Type tCommand, CallOptions opts)
    {
        if (!_executorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((IVoidCommandExecutor)executor).ExecuteVoid(cmd, opts);
    }

    /// <summary>
    /// register a "unary" command (<see cref="ICommand{TResult}"/>) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public void Register<TCommand, TResult>() where TCommand : class, ICommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        RemoteMap[tCommand] = this;
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
        RemoteMap[tCommand] = this;
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
        RemoteMap[tCommand] = this;
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