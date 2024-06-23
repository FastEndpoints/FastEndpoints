using FastEndpoints.Messaging.Remote;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

abstract class EventHubBase
{
    //key: tEvent
    //val: event hub for the event type
    //values get created when the DI container resolves each event hub type and the ctor is run.
    protected static readonly ConcurrentDictionary<Type, EventHubBase> AllHubs = new();

    protected abstract Task BroadcastEvent(IEvent evnt, CancellationToken ct);

    internal static Task AddToSubscriberQueues(IEvent evnt, CancellationToken ct)
    {
        var tEvent = evnt.GetType();

        return AllHubs.TryGetValue(tEvent, out var hub)
                   ? hub.BroadcastEvent(evnt, ct)
                   : throw new InvalidOperationException($"An event hub has not been registered for [{tEvent.FullName}]");
    }
}

sealed class EventHub<TEvent, TStorageRecord, TStorageProvider> : EventHubBase, IMethodBinder<EventHub<TEvent, TStorageRecord, TStorageProvider>>
    where TEvent : class, IEvent
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
{
    internal static HubMode Mode = HubMode.EventPublisher;

    //key: subscriber id
    //val: subscriber object
    static readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();
    static readonly Type _tEvent = typeof(TEvent);
    static bool _isRoundRobinMode;
    static TStorageProvider? _storage;
    static readonly object _lock = new();

    string? _lastReceivedBy;
    readonly bool _isInMemoryProvider;
    readonly EventHubExceptionReceiver? _errors;
    readonly ILogger _logger;

    public EventHub(IServiceProvider svcProvider)
    {
        AllHubs[_tEvent] = this;
        _isRoundRobinMode = Mode.HasFlag(HubMode.RoundRobin);
        _storage ??= (TStorageProvider)ActivatorUtilities.CreateInstance(svcProvider, typeof(TStorageProvider));
        _isInMemoryProvider = _storage is InMemoryEventHubStorage;
        EventHubStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //for stale record purging task setup
        EventHubStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _isInMemoryProvider;
        _errors = svcProvider.GetService<EventHubExceptionReceiver>();
        _logger = svcProvider.GetRequiredService<ILogger<EventHub<TEvent, TStorageRecord, TStorageProvider>>>();

        var t = _storage.RestoreSubscriberIDsForEventTypeAsync(
            new()
            {
                CancellationToken = CancellationToken.None,
                EventType = _tEvent.FullName!,
                Match = e => e.EventType == _tEvent.FullName! && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn,
                Projection = e => e.SubscriberID
            });

        while (!t.IsCompleted)
            Thread.Sleep(100);

        foreach (var subID in t.Result)
            _subscribers[subID] = new();
    }

    static readonly string[] _httPost = { "POST" };

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent, TStorageRecord, TStorageProvider>> ctx)
    {
        var metadata = new List<object>();
        var eventAttributes = _tEvent.GetCustomAttributes(false);
        if (eventAttributes.Length > 0)
            metadata.AddRange(eventAttributes);
        metadata.Add(new HttpMethodMetadata(_httPost, acceptCorsPreflight: true));

        var sub = new Method<string, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: _tEvent.FullName!,
            name: "sub",
            requestMarshaller: new MessagePackMarshaller<string>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        ctx.AddServerStreamingMethod(sub, metadata, OnSubscriberConnected);

        if (!Mode.HasFlag(HubMode.EventBroker))
            return;

        var pub = new Method<TEvent, EmptyObject>(
            type: MethodType.Unary,
            serviceName: _tEvent.FullName!,
            name: "pub",
            requestMarshaller: new MessagePackMarshaller<TEvent>(),
            responseMarshaller: new MessagePackMarshaller<EmptyObject>());

        ctx.AddUnaryMethod(pub, metadata, OnEventReceived);
    }

    //internal to allow unit testing
    internal async Task OnSubscriberConnected(EventHub<TEvent, TStorageRecord, TStorageProvider> _,
                                              string subscriberID,
                                              IServerStreamWriter<TEvent> stream,
                                              ServerCallContext ctx)
    {
        _logger.SubscriberConnected(subscriberID, _tEvent.FullName!);

        var subscriber = _subscribers.GetOrAdd(subscriberID, new Subscriber());
        subscriber.IsConnected = true;
        var retrievalErrorCount = 0;
        var updateErrorCount = 0;

        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await _storage!.GetNextBatchAsync(
                              new()
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
                _logger.StorageGetNextBatchError(subscriberID, _tEvent.FullName!, ex.Message);
                await Task.Delay(5000);

                continue;
            }

            if (records.Any())
            {
                foreach (var record in records)
                {
                    try
                    {
                        await stream.WriteAsync(record.GetEvent<TEvent>(), ctx.CancellationToken);
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
                                //it's either cancelled or queue is full
                                //ignore and discard event if queue is full
                            }
                        }

                        if (_isRoundRobinMode)
                            subscriber.IsConnected = false;

                        return; //stream is most likely broken/cancelled. exit the method here and let the subscriber re-connect and re-enter the method.
                    }

                    while (!_isInMemoryProvider && !ctx.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            record.IsComplete = true;
                            await _storage.MarkEventAsCompleteAsync(record, ctx.CancellationToken);
                            updateErrorCount = 0;

                            break;
                        }
                        catch (Exception ex)
                        {
                            updateErrorCount++;
                            _errors?.OnMarkEventAsCompleteError<TEvent>(record, updateErrorCount, ex, ctx.CancellationToken);
                            _logger.StorageMarkAsCompleteError(subscriberID, _tEvent.FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                }
            }
            else
            {
                //wait until either the semaphore is released or a minute has elapsed
                await Task.WhenAny(subscriber.Sem.WaitAsync(ctx.CancellationToken), Task.Delay(60000));
            }
        }

        //mark subscriber as disconnected if the while loop is exited.
        //which means the subscriber either cancelled or stream got broken.
        if (_isRoundRobinMode)
            subscriber.IsConnected = false;
    }

    IEnumerable<string> GetReceiveCandidates()
    {
        if (_isRoundRobinMode)
        {
            var connectedSubIds = _subscribers
                                  .Where(kv => kv.Value.IsConnected)
                                  .Select(kv => kv.Key)
                                  .ToArray(); //take a snapshot of currently connected subscriber ids

            if (connectedSubIds.Length <= 1)
                return connectedSubIds;

            IEnumerable<string> qualified;

            lock (_lock)
            {
                qualified = connectedSubIds.SkipWhile(s => s == _lastReceivedBy).Take(1);
                _lastReceivedBy = qualified.Single();
            }

            return qualified;
        }

        return _subscribers.Keys;
    }

    protected override async Task BroadcastEvent(IEvent evnt, CancellationToken ct)
    {
        var subscribers = GetReceiveCandidates();

        var startTime = DateTime.Now;

        while (!subscribers.Any())
        {
            _logger.NoSubscribersWarning(_tEvent.FullName!);
        #pragma warning disable CA2016

            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(5000);
        #pragma warning restore CA2016
            if (ct.IsCancellationRequested || (DateTime.Now - startTime).TotalSeconds >= 60)
                break;
        }

        var createErrorCount = 0;

        foreach (var subId in subscribers)
        {
            var record = new TStorageRecord
            {
                SubscriberID = subId,
                EventType = _tEvent.FullName!,
                ExpireOn = DateTime.UtcNow.AddHours(4)
            };
            record.SetEvent((TEvent)evnt);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _storage!.StoreEventAsync(record, ct);
                    _subscribers[subId].Sem.Release();
                    createErrorCount = 0;

                    break;
                }
                catch (OverflowException)
                {
                    _subscribers.Remove(subId, out var sub);
                    sub?.Sem.Dispose();
                    _errors?.OnInMemoryQueueOverflow<TEvent>(record, ct);
                    _logger.QueueOverflowWarning(subId, _tEvent.FullName!);

                    break;
                }
                catch (Exception ex)
                {
                    createErrorCount++;
                    _errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, ct);
                    _logger.StoreEventError(subId, _tEvent.FullName!, ex.Message);
                #pragma warning disable CA2016

                    // ReSharper disable once MethodSupportsCancellation
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

    class Subscriber
    {
        public SemaphoreSlim Sem { get; } //semaphorslim for waiting on record availability
        public bool IsConnected { get; set; }

        public Subscriber()
        {
            Sem = new(0);
        }
    }
}