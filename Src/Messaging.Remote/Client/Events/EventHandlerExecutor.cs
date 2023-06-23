using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

internal sealed class EventHandlerExecutor<TEvent, TEventHandler> : BaseCommandExecutor<string, TEvent>, ICommandExecutor
    where TEvent : class, IEvent
    where TEventHandler : IEventHandler<TEvent>
{
    private readonly ObjectFactory _handlerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<EventHandlerExecutor<TEvent, TEventHandler>>? _logger;
    private readonly string _subscriberID;

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
        _subscriberID = (Environment.MachineName + GetType().FullName + channel.Target).ToHash();
    }

    internal void Start(CallOptions opts)
    {
        eventProducerTask ??= Task.Factory.StartNew(async () =>
        {
            var call = _invoker.AsyncServerStreamingCall(_method, null, opts, _subscriberID);

            while (!opts.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    while (await call.ResponseStream.MoveNext(opts.CancellationToken)) // actual network call happens on MoveNext()
                    {
                        var record = EventSubscriberStorage.RecordFactory();
                        record.SubscriberID = _subscriberID;
                        record.Event = call.ResponseStream.Current;
                        record.EventType = typeof(TEvent).FullName!;

                        while (!opts.CancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                await EventSubscriberStorage.Provider.StoreEventAsync(record, opts.CancellationToken);
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("Event storage 'create' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                                    _subscriberID,
                                    typeof(TEvent).FullName,
                                    ex.Message);
                                await Task.Delay(5000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogTrace("Event 'receive' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                        _subscriberID,
                        typeof(TEvent),
                        ex.Message);
                    call.Dispose(); //the stream is most likely broken, so dispose it and initialize a new call
                    await Task.Delay(5000);
                    call = _invoker.AsyncServerStreamingCall(_method, null, opts, _subscriberID);
                }
            }
        }, TaskCreationOptions.LongRunning);

        eventConsumerTask ??= Task.Factory.StartNew(async () =>
        {
            while (!opts.CancellationToken.IsCancellationRequested)
            {
                IEventStorageRecord? evntRecord;

                try
                {
                    evntRecord = await EventSubscriberStorage.Provider.GetNextEventAsync(_subscriberID, opts.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Event storage 'retrieval' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                        _subscriberID,
                        typeof(TEvent).FullName,
                        ex.Message);
                    await Task.Delay(5000);
                    continue;
                }

                if (evntRecord is not null)
                {
                    var handler = (TEventHandler)_handlerFactory(_serviceProvider!, null);

                    try
                    {
                        await handler.HandleAsync((TEvent)evntRecord.Event, opts.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogCritical("Event [{event}] execution error: [{err}]. Retrying after 5 seconds...",
                            typeof(TEvent).FullName,
                            ex.Message);
                        await Task.Delay(5000);
                        continue;
                    }

                    while (!opts.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await EventSubscriberStorage.Provider.MarkEventAsCompleteAsync(evntRecord, opts.CancellationToken);
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Event storage 'update' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                                _subscriberID,
                                typeof(TEvent).FullName,
                                ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                }
                else
                {
                    await Task.Delay(300);
                }
            }
        }, TaskCreationOptions.LongRunning);
    }
}