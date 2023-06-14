using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal sealed class EventHandlerExecutor<TEvent, TEventHandler> : BaseCommandExecutor<EmptyObject, TEvent>, ICommandExecutor
    where TEvent : class, IEvent
    where TEventHandler : IEventHandler<TEvent>
{
    private readonly ObjectFactory _handlerFactory;
    private readonly IServiceProvider? _serviceProvider;

#pragma warning disable IDE0052
    private Task? workerTask;
#pragma warning restore IDE0052

    internal EventHandlerExecutor(GrpcChannel channel, IServiceProvider? serviceProvider)
        : base(channel, MethodType.ServerStreaming, $"{typeof(TEvent).FullName}")
    {
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _serviceProvider = serviceProvider;
    }

    internal void Start()
    {
        workerTask ??= Task.Factory.StartNew(async () =>
        {
            var stream = _invoker.AsyncServerStreamingCall(_method, null, new CallOptions(), new EmptyObject()).ResponseStream;

            while (await stream.MoveNext())
            {
                var handler = (TEventHandler)_handlerFactory(_serviceProvider!, null);
                await handler.HandleAsync(stream.Current, default);
            }
        }, TaskCreationOptions.LongRunning);
    }
}