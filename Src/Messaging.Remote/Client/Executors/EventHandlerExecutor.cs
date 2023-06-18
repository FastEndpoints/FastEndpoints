using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventHandlerExecutor<TEvent, TEventHandler> : BaseCommandExecutor<string, TEvent>, ICommandExecutor
    where TEvent : class, IEvent
    where TEventHandler : IEventHandler<TEvent>
{
    private readonly ObjectFactory _handlerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private const int queueSizeLimit = 1000;
    private readonly ConcurrentQueue<TEvent> _events = new();
    private readonly ILogger<EventHandlerExecutor<TEvent, TEventHandler>>? _logger;

#pragma warning disable IDE0052
    private Task? eventProducerTask;
    private Task? eventConsumerTask;
#pragma warning restore IDE0052

    internal EventHandlerExecutor(GrpcChannel channel, IServiceProvider? serviceProvider)
        : base(channel, MethodType.ServerStreaming, $"{typeof(TEvent).FullName}")
    {
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _serviceProvider = serviceProvider;
        _logger = serviceProvider?.GetRequiredService<ILogger<EventHandlerExecutor<TEvent, TEventHandler>>>();
    }

    internal void Start(CallOptions opts)
    {
        eventProducerTask ??= Task.Factory.StartNew(async () =>
        {
            var subscriberID = (Environment.MachineName + GetType().FullName).ToHash();
            var call = _invoker.AsyncServerStreamingCall(_method, null, opts, subscriberID);

            while (true)
            {
                if (_events.Count >= queueSizeLimit)
                {
                    _logger?.LogWarning("Event receive queue for [id:{subscriber}({event})] is full! Resuming after 10 seconds...", subscriberID, typeof(TEvent));
                    await Task.Delay(10000);
                    continue;
                }

                try
                {
                    while (_events.Count < queueSizeLimit && await call.ResponseStream.MoveNext()) // actual network call happens on MoveNext()
                    {
                        _events.Enqueue(call.ResponseStream.Current);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Event receive error in [id:{subscriber}({event})]: [{err}]. Retrying after 10 seconds...", subscriberID, typeof(TEvent), ex.Message);
                    call.Dispose(); //the stream is most likely broken, so dispose it and initialize a new call
                    call = _invoker.AsyncServerStreamingCall(_method, null, opts, subscriberID);
                    await Task.Delay(10000);
                }
            }
        }, TaskCreationOptions.LongRunning);

        eventConsumerTask ??= Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                try
                {
                    if (!_events.IsEmpty && _events.TryPeek(out var evnt))
                    {
                        var handler = (TEventHandler)_handlerFactory(_serviceProvider!, null);
                        await handler.HandleAsync(evnt, opts.CancellationToken);
                        while (!_events.TryDequeue(out var _))
                            await Task.Delay(100);
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical("Event [{event}] execution error: [{err}]. Retrying after 10 seconds...", typeof(TEvent).FullName, ex.Message);
                    await Task.Delay(10000);
                }
            }
        }, TaskCreationOptions.LongRunning);
    }
}