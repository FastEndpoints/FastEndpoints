using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

internal sealed class EventSubscriber<TEvent, TEventHandler> : BaseCommandExecutor<string, TEvent>, ICommandExecutor
    where TEvent : class, IEvent
    where TEventHandler : IEventHandler<TEvent>
{
    private readonly ObjectFactory _handlerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private readonly SubscriberExceptionReceiver? _errorReceiver;
    private readonly ILogger<EventSubscriber<TEvent, TEventHandler>>? _logger;
    private readonly string _subscriberID;

    internal EventSubscriber(GrpcChannel channel, IServiceProvider? serviceProvider)
        : base(channel: channel,
               methodType: MethodType.ServerStreaming,
               endpointName: $"{typeof(TEvent).FullName}/sub")
    {
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _serviceProvider = serviceProvider;
        _errorReceiver = _serviceProvider?.GetService<SubscriberExceptionReceiver>();
        _logger = serviceProvider?.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler>>>();
        _subscriberID = (Environment.MachineName + GetType().FullName + channel.Target).ToHash();
        _logger?.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, typeof(TEvent).FullName!);
    }

    internal void Start(CallOptions opts)
    {
        _ = EventReceiver(opts, _invoker, _method, _subscriberID, _logger, _errorReceiver);
        _ = EventExecutor(opts, _subscriberID, _logger, _handlerFactory, _serviceProvider, _errorReceiver);
    }

    private static async Task EventReceiver(CallOptions opts, CallInvoker invoker, Method<string, TEvent> method, string subscriberID, ILogger? logger, SubscriberExceptionReceiver? errors)
    {
        var call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
        var createErrorCount = 0;
        var receiveErrorCount = 0;

        while (!opts.CancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(opts.CancellationToken)) // actual network call happens on MoveNext()
                {
                    var record = EventSubscriberStorage.RecordFactory();
                    record.SubscriberID = subscriberID;
                    record.Event = call.ResponseStream.Current;
                    record.EventType = typeof(TEvent).FullName!;
                    record.ExpireOn = DateTime.UtcNow.AddHours(4);

                    while (!opts.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await EventSubscriberStorage.Provider.StoreEventAsync(record, opts.CancellationToken);
                            createErrorCount = 0;
                            break;
                        }
                        catch (Exception ex)
                        {
                            createErrorCount++;
                            _ = errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, opts.CancellationToken);
                            logger?.StorageCreateError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                    receiveErrorCount = 0;
                }
            }
            catch (Exception ex)
            {
                receiveErrorCount++;
                _ = errors?.OnEventReceiveError<TEvent>(subscriberID, receiveErrorCount, ex, opts.CancellationToken);
                logger?.ReceiveTrace(subscriberID, typeof(TEvent).FullName!, ex.Message);
                call.Dispose(); //the stream is most likely broken, so dispose it and initialize a new call
                await Task.Delay(5000);
                call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
            }
        }
    }

    private static async Task EventExecutor(CallOptions opts, string subscriberID, ILogger? logger, ObjectFactory handlerFactory, IServiceProvider? serviceProvider, SubscriberExceptionReceiver? errors)
    {
        var retrievalErrorCount = 0;
        var executionErrorCount = 0;
        var updateErrorCount = 0;

        while (!opts.CancellationToken.IsCancellationRequested)
        {
            IEventStorageRecord? evntRecord;

            try
            {
                evntRecord = await EventSubscriberStorage.Provider.GetNextEventAsync(subscriberID, opts.CancellationToken);
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                retrievalErrorCount++;
                _ = errors?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, opts.CancellationToken);
                logger?.StorageRetrieveError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                await Task.Delay(5000);
                continue;
            }

            if (evntRecord is not null)
            {
                var handler = (TEventHandler)handlerFactory(serviceProvider!, null);

                try
                {
                    await handler.HandleAsync((TEvent)evntRecord.Event, opts.CancellationToken);
                    executionErrorCount = 0;
                }
                catch (Exception ex)
                {
                    executionErrorCount++;
                    _ = errors?.OnHandlerExecutionError<TEvent, TEventHandler>(evntRecord, executionErrorCount, ex, opts.CancellationToken);
                    logger?.HandlerExecutionCritial(typeof(TEvent).FullName!, ex.Message);
                    await Task.Delay(5000);
                    continue;
                }

                while (!opts.CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await EventSubscriberStorage.Provider.MarkEventAsCompleteAsync(evntRecord, opts.CancellationToken);
                        updateErrorCount = 0;
                        break;
                    }
                    catch (Exception ex)
                    {
                        updateErrorCount++;
                        _ = errors?.OnMarkEventAsCompleteError<TEvent>(evntRecord, updateErrorCount, ex, opts.CancellationToken);
                        logger?.StorageUpdateError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                        await Task.Delay(5000);
                    }
                }
            }
            else
            {
                await Task.Delay(300);
            }
        }
    }
}