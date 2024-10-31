using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Grpc.Net.Client.Web;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// represents a connection to a remote server that hosts command and event handlers
/// </summary>
public class RemoteConnectionCore
{
    //key: tCommand or tEventHandler
    //val: remote server that hosts command handlers/event buses
    internal static ConcurrentDictionary<Type, RemoteConnectionCore> RemoteMap { get; } = new(); //concurrent is needed due to parallel integration tests

    internal static Type StorageRecordType { private get; set; } = typeof(InMemoryEventStorageRecord);
    internal static Type StorageProviderType { private get; set; } = typeof(InMemoryEventSubscriberStorage);

    //key: tCommand
    //val: command executor
    /// <summary />
    protected readonly Dictionary<Type, ICommandExecutor> ExecutorMap = new();

    /// <summary />
    protected GrpcChannel? Channel;

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// grpc channel settings
    /// </summary>
    public GrpcChannelOptions ChannelOptions { get; set; } = new()
    {
        ServiceConfig = new()
        {
            MethodConfigs =
            {
                new()
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
                }
            }
        },
        MaxRetryAttempts = 5
    };

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// the address of the remote server
    /// </summary>
    public string RemoteAddress { get; }

    /// <summary />
    protected readonly IServiceProvider ServiceProvider;

    internal RemoteConnectionCore(string address, IServiceProvider serviceProvider)
    {
        RemoteAddress = address;
        ServiceProvider = serviceProvider;

        var httpMsgHnd = (HttpMessageHandler)serviceProvider.GetService(typeof(HttpMessageHandler));
        ChannelOptions.HttpHandler = httpMsgHnd ?? new GrpcWebHandler(new HttpClientHandler()) { GrpcWebMode = GrpcWebMode.GrpcWebText };
    }

    /// <summary>
    /// register a "void" command (<see cref="ICommand" />) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    public void Register<TCommand>() where TCommand : class, ICommand
    {
        var tCommand = typeof(TCommand);
        RemoteMap[tCommand] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        ExecutorMap[tCommand] = new VoidCommandExecutor<TCommand>(Channel);
    }

    internal Task ExecuteVoid(ICommand cmd, Type tCommand, CallOptions opts)
        => !ExecutorMap.TryGetValue(tCommand, out var executor)
               ? throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]")
               : ((IVoidCommandExecutor)executor).ExecuteVoid(cmd, opts);

    /// <summary>
    /// register a "unary" command (<see cref="ICommand{TResult}" />) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public void Register<TCommand, TResult>() where TCommand : class, ICommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        RemoteMap[tCommand] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        ExecutorMap[tCommand] = new UnaryCommandExecutor<TCommand, TResult>(Channel);
    }

    internal Task<TResult> ExecuteUnary<TResult>(ICommand<TResult> cmd, Type tCommand, CallOptions opts) where TResult : class
        => !ExecutorMap.TryGetValue(tCommand, out var executor)
               ? throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]")
               : ((IUnaryCommandExecutor<TResult>)executor).ExecuteUnary(cmd, opts);

    /// <summary>
    /// register a "server stream" command (<see cref="IServerStreamCommand{TResult}" />) for this remote connection where the handler for it is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result stream</typeparam>
    public void RegisterServerStream<TCommand, TResult>() where TCommand : class, IServerStreamCommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);
        RemoteMap[tCommand] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        ExecutorMap[tCommand] = new ServerStreamCommandExecutor<TCommand, TResult>(Channel);
    }

    internal IAsyncStreamReader<TResult> ExecuteServerStream<TResult>(IServerStreamCommand<TResult> cmd, Type tCommand, CallOptions opts)
        where TResult : class
        => !ExecutorMap.TryGetValue(tCommand, out var executor)
               ? throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]")
               : ((IServerStreamCommandExecutor<TResult>)executor).ExecuteServerStream(cmd, opts);

    /// <summary>
    /// register a remote handler for a "client stream" (<see cref="IAsyncEnumerable{T}" />) for this remote connection.
    /// </summary>
    /// <typeparam name="T">the type of the items in the stream</typeparam>
    /// <typeparam name="TResult">the type of the result that will be received when the stream ends</typeparam>
    public void RegisterClientStream<T, TResult>() where T : class where TResult : class
    {
        var tCommand = typeof(IAsyncEnumerable<T>);
        RemoteMap[tCommand] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        ExecutorMap[tCommand] = new ClientStreamCommandExecutor<T, TResult>(Channel);
    }

    internal Task<TResult> ExecuteClientStream<T, TResult>(IAsyncEnumerable<T> cmd, Type tCommand, CallOptions opts) where T : class where TResult : class
        => !ExecutorMap.TryGetValue(tCommand, out var executor)
               ? throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]")
               : ((IClientStreamCommandExecutor<T, TResult>)executor).ExecuteClientStream(cmd, opts);

    /// <summary>
    /// subscribe to a broadcast channel for a given event type (<typeparamref name="TEvent" />) on the remote host.
    /// the received events will be handled by the specified handler (<typeparamref name="TEventHandler" />) on this machine.
    /// </summary>
    /// <typeparam name="TEvent">the type of the events that will be received</typeparam>
    /// <typeparam name="TEventHandler">the handler that will be handling the received events</typeparam>
    /// <param name="callOptions">the call options</param>
    /// <param name="clientIdentifier">
    /// a unique identifier for this client. this will be used to create a durable subscriber id which will allow the server to
    /// uniquely identify this subscriber/client across disconnections. if you don't set this value, only one subscriber from a single machine is possible.
    /// i.e. if you spin up multiple instances of this subscriber they will all connect to the server with the same subscriber id, which will result in
    /// unpredictable event receiving behavior.
    /// </param>
    public void Subscribe<TEvent, TEventHandler>(CallOptions callOptions = default, string clientIdentifier = "default")
        where TEvent : class, IEvent
        where TEventHandler : IEventHandler<TEvent>
    {
        var tEventHandler = typeof(TEventHandler);
        RemoteMap[tEventHandler] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);

        var tHandler = ServiceProvider.GetService<IEventHandler<TEvent>>()?.GetType() ?? typeof(TEventHandler);

        var tEventSubscriber = typeof(EventSubscriber<,,,>).MakeGenericType(
            typeof(TEvent),
            tHandler,
            StorageRecordType,
            StorageProviderType);

        var eventSubscriber = (ICommandExecutor)ActivatorUtilities.CreateInstance(ServiceProvider, tEventSubscriber, Channel, clientIdentifier);
        ExecutorMap[tEventHandler] = eventSubscriber;
        ((IEventSubscriber)eventSubscriber).Start(callOptions);
    }
}