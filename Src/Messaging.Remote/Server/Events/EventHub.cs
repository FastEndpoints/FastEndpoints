using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal abstract class EventHubBase
{
    //key: tEvent
    //val: event hub for the event type
    //values get created when the DI container resolves each event hub type and the ctor is run.
    protected static readonly ConcurrentDictionary<Type, EventHubBase> _allHubs = new();

    protected abstract Task BroadcastEvent(object evnt, CancellationToken ct);

    internal static Task AddToSubscriberQueues(IEvent evnt, CancellationToken ct)
    {
        var tEvent = evnt.GetType();

        if (!_allHubs.TryGetValue(tEvent, out var hub))
            throw new InvalidOperationException($"An event hub has not been registered for [{tEvent.FullName}]");

        return hub.BroadcastEvent(evnt, ct);
    }
}

internal sealed class EventHub<TEvent, TStorageRecord, TStorageProvider> : EventHubBase, IMethodBinder<EventHub<TEvent, TStorageRecord, TStorageProvider>>
    where TEvent : class, IEvent
    where TStorageRecord : IEventStorageRecord, new()
    where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
{
    internal static HubMode Mode = HubMode.EventPublisher;

    //key: subscriber id
    //val: semaphorslim for waiting on record availability
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _subscribers = new();
    private static readonly Type _tEvent = typeof(TEvent);
    private static TStorageProvider? _storage;

    private readonly bool _isInMemoryProvider;
    private readonly EventHubExceptionReceiver? _errors;
    private readonly ILogger _logger;

    public EventHub(IServiceProvider svcProvider)
    {
        _allHubs[_tEvent] = this;
        _storage ??= (TStorageProvider)ActivatorUtilities.CreateInstance(svcProvider, typeof(TStorageProvider));
        _isInMemoryProvider = _storage is InMemoryEventHubStorage;
        EventHubStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //for stale record purging task setup
        EventHubStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _isInMemoryProvider;
        _errors = svcProvider.GetService<EventHubExceptionReceiver>();
        _logger = svcProvider.GetRequiredService<ILogger<EventHub<TEvent, TStorageRecord, TStorageProvider>>>();

        var t = _storage.RestoreSubscriberIDsForEventTypeAsync(new()
        {
            CancellationToken = CancellationToken.None,
            EventType = _tEvent.FullName!,
            Match = e => e.EventType == _tEvent.FullName! && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn,
            Projection = e => e.SubscriberID
        });

        while (!t.IsCompleted)
            Thread.Sleep(100);

        foreach (var subID in t.Result)
            _subscribers[subID] = new(0);
    }

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent, TStorageRecord, TStorageProvider>> ctx)
    {
        var metadata = new List<object>();
        var eventAttributes = _tEvent.GetCustomAttributes(false);
        if (eventAttributes?.Length > 0) metadata.AddRange(eventAttributes);
        metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

        var sub = new Method<string, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: _tEvent.FullName!,
            name: "sub",
            requestMarshaller: new MessagePackMarshaller<string>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        ctx.AddServerStreamingMethod(sub, metadata, OnSubscriberConnected);

        if (Mode is HubMode.EventBroker)
        {
            var pub = new Method<TEvent, EmptyObject>(
                type: MethodType.Unary,
                serviceName: _tEvent.FullName!,
                name: "pub",
                requestMarshaller: new MessagePackMarshaller<TEvent>(),
                responseMarshaller: new MessagePackMarshaller<EmptyObject>());

            ctx.AddUnaryMethod(pub, metadata, OnEventReceived);
        }
    }

    //internal to allow unit testing
    internal async Task OnSubscriberConnected(EventHub<TEvent, TStorageRecord, TStorageProvider> _, string subscriberID, IServerStreamWriter<TEvent> stream, ServerCallContext ctx)
    {
        _logger.SubscriberConnected(subscriberID, _tEvent.FullName!);

        var sem = _subscribers.GetOrAdd(subscriberID, new SemaphoreSlim(0));
        var retrievalErrorCount = 0;
        var updateErrorCount = 0;
        IEnumerable<TStorageRecord> records;

        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            try
            {
                records = await _storage!.GetNextBatchAsync(new()
                {
                    CancellationToken = ctx.CancellationToken,
                    Limit = 25,
                    SubscriberID = subscriberID,
                    Match = e => e.SubscriberID == subscriberID && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn
                });
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                retrievalErrorCount++;
                _errors?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, ctx.CancellationToken);
                _logger.StorageRetrieveError(subscriberID, _tEvent.FullName!, ex.Message);
                await Task.Delay(5000);
                continue;
            }

            if (records.Any())
            {
                foreach (var record in records)
                {
                    try
                    {
                        await stream.WriteAsync((TEvent)record.Event, ctx.CancellationToken);
                    }
                    catch
                    {
                        if (_isInMemoryProvider)
                        {
                            try
                            {
                                await _storage.StoreEventAsync(record, ctx.CancellationToken);
                            }
                            catch
                            {
                                //ignore and discard event if queue is full
                            }
                        }
                        return; //stream is most likely broken/cancelled. let the client re-connect.
                    }

                    while (!_isInMemoryProvider && !ctx.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _storage.MarkEventAsCompleteAsync(record, ctx.CancellationToken);
                            updateErrorCount = 0;
                            break;
                        }
                        catch (Exception ex)
                        {
                            updateErrorCount++;
                            _errors?.OnMarkEventAsCompleteError<TEvent>(record, updateErrorCount, ex, ctx.CancellationToken);
                            _logger.StorageUpdateError(subscriberID, _tEvent.FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                }
            }
            else
            {
                await sem.WaitAsync(ctx.CancellationToken); //this blocks until new records are stored (semaphore released)
            }
        }
    }

    protected async override Task BroadcastEvent(object evnt, CancellationToken ct)
    {
        var createErrorCount = 0;

        foreach (var subId in _subscribers.Keys)
        {
            var record = new TStorageRecord
            {
                SubscriberID = subId,
                Event = evnt,
                EventType = _tEvent.FullName!,
                ExpireOn = DateTime.UtcNow.AddHours(4)
            };

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _storage!.StoreEventAsync(record, ct);
                    _subscribers[subId].Release();
                    createErrorCount = 0;
                    break;
                }
                catch (OverflowException)
                {
                    _subscribers.Remove(subId, out var sem);
                    sem?.Dispose();
                    _errors?.OnInMemoryQueueOverflow<TEvent>(record, ct);
                    _logger.QueueOverflowWarning(subId, _tEvent.FullName!);
                    break;
                }
                catch (Exception ex)
                {
                    createErrorCount++;
                    _errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, ct);
                    _logger.StorageCreateError(subId, _tEvent.FullName!, ex.Message);
#pragma warning disable CA2016
                    await Task.Delay(5000);
#pragma warning restore CA2016
                }
            }
        }
    }

    //internal to allow unit testing
    internal Task<EmptyObject> OnEventReceived(EventHub<TEvent, TStorageRecord, TStorageProvider> __, TEvent evnt, ServerCallContext ctx)
    {
        _ = AddToSubscriberQueues(evnt, ctx.CancellationToken);
        return Task.FromResult(EmptyObject.Instance);
    }
}