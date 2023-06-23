using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// handler registration options
/// </summary>
public sealed class HandlerOptions
{
    private readonly IEndpointRouteBuilder routeBuilder;

    internal HandlerOptions(IEndpointRouteBuilder builder)
    {
        routeBuilder = builder;
    }

    /// <summary>
    /// registers a "void" command handler this server is hosting.
    /// </summary>
    /// <typeparam name="TCommand">the type of the incoming command</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming command</typeparam>
    public GrpcServiceEndpointConventionBuilder Register<TCommand, THandler>()
        where TCommand : class, ICommand
        where THandler : class, ICommandHandler<TCommand>
            => routeBuilder.MapGrpcService<VoidHandlerExecutor<TCommand, THandler>>();

    /// <summary>
    /// registers a "unary" command handler this server is hosting.
    /// </summary>
    /// <typeparam name="TCommand">the type of the incoming command</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming command</typeparam>
    /// <typeparam name="TResult">the type of the result that will be returned from the handler</typeparam>
    public GrpcServiceEndpointConventionBuilder Register<TCommand, THandler, TResult>()
        where TCommand : class, ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TResult : class
            => routeBuilder.MapGrpcService<UnaryHandlerExecutor<TCommand, THandler, TResult>>();

    /// <summary>
    /// registers a "server stream" command handler this server is hosting.
    /// </summary>
    /// <typeparam name="TCommand">the type of the incoming command</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming command</typeparam>
    /// <typeparam name="TResult">the type of the result stream that will be returned from the handler</typeparam>
    public GrpcServiceEndpointConventionBuilder RegisterServerStream<TCommand, THandler, TResult>()
        where TCommand : class, IServerStreamCommand<TResult>
        where THandler : class, IServerStreamCommandHandler<TCommand, TResult>
        where TResult : class
            => routeBuilder.MapGrpcService<ServerStreamHandlerExecutor<TCommand, THandler, TResult>>();

    /// <summary>
    /// registers a "client stream" command handler this server is hosting.
    /// </summary>
    /// <typeparam name="T">the type of the incoming item stream</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming stream</typeparam>
    /// <typeparam name="TResult">the type of the result that will be returned from the handler when the stream ends</typeparam>
    public GrpcServiceEndpointConventionBuilder RegisterClientStream<T, THandler, TResult>()
        where T : class
        where THandler : class, IClientStreamCommandHandler<T, TResult>
        where TResult : class
            => routeBuilder.MapGrpcService<ClientStreamHandlerExecutor<T, THandler, TResult>>();

    /// <summary>
    /// register a custom storage provider for event publishers
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of the event storage record</typeparam>
    /// <typeparam name="TStorageProvider">the type of the event storage provider</typeparam>
    public void EventPublisherStorageProvider<TStorageRecord, TStorageProvider>()
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventPublisherStorageProvider
            => EventPublisherStorage.Initialize<TStorageRecord, TStorageProvider>(routeBuilder.ServiceProvider);

    /// <summary>
    /// registers an "event hub" that broadcasts events of the given type to all remote subscribers in an asynchronous manner
    /// </summary>
    /// <typeparam name="TEvent">the type of the event hub</typeparam>
    public GrpcServiceEndpointConventionBuilder RegisterEventHub<TEvent>()
        where TEvent : class, IEvent
    {
        if (!EventPublisherStorage.IsInitialized)
            EventPublisherStorage.Initialize<InMemoryEventStorageRecord, InMemoryEventPublisherStorage>(routeBuilder.ServiceProvider);

        //there's no DI for EventHub<TEvent> :-(
        EventHub<TEvent>.Logger ??= routeBuilder.ServiceProvider.GetRequiredService<ILogger<EventHub<TEvent>>>();

        return routeBuilder.MapGrpcService<EventHub<TEvent>>();
    }
}