// ReSharper disable MethodSupportsCancellation

using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

sealed class EventHub<TEvent, TStorageRecord, TStorageProvider> : EventHubBase, IMethodBinder<EventHub<TEvent, TStorageRecord, TStorageProvider>>
    where TEvent : class, IEvent
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
{
    internal static HubMode Mode = HubMode.EventPublisher;

    static readonly Type _tEvent = typeof(TEvent);
    static readonly SubscriberRegistry _registry = new();
    static ISubscriberSelector _selector = new FanOutSelector();
    static TStorageProvider? _storage;

    static HubStorageBehavior _storageBehavior = HubStorageBehavior.InMemory;

    readonly HubContext _ctx;
    readonly IEventReceiver<TEvent>? _testEventReceiver;

    internal static void Configure(HubMode mode, IEnumerable<string>? knownSubscriberIDs = null)
    {
        if (mode.HasFlag(HubMode.RoundRobin) && knownSubscriberIDs?.Any() == true)
        {
            throw new InvalidOperationException(
                $"Known subscriber IDs are not supported for round-robin event hub [{_tEvent.FullName!}]. Round-robin hubs only dispatch to currently connected subscribers.");
        }

        Mode = mode;
        _selector = mode.HasFlag(HubMode.RoundRobin) ? new RoundRobinSelector() : new FanOutSelector();
        _registry.Configure(knownSubscriberIDs);
    }

    public EventHub(IServiceProvider svcProvider)
    {
        AllHubs[_tEvent] = this;
        _selector = Mode.HasFlag(HubMode.RoundRobin) ? new RoundRobinSelector() : new FanOutSelector(); // for cases where Mode is set directly (tests) without calling Configure
        _storage ??= (TStorageProvider)ActivatorUtilities.CreateInstance(svcProvider, typeof(TStorageProvider));
        _storageBehavior = HubStorageBehavior.For(_storage);
        IsInMemoryProvider = _storageBehavior == HubStorageBehavior.InMemory;
        EventHubStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //for stale record purging task setup
        EventHubStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = IsInMemoryProvider;
        _ctx = new(
            svcProvider.GetRequiredService<ILogger<EventHub<TEvent, TStorageRecord, TStorageProvider>>>(),
            svcProvider.GetService<EventHubExceptionReceiver>(),
            _tEvent.FullName!,
            svcProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        _testEventReceiver = svcProvider.GetService<IEventReceiver<TEvent>>();
    }

    protected override async Task Initialize()
    {
        ArgumentNullException.ThrowIfNull(_storage);

        using var timeoutCts = new CancellationTokenSource(EventHubSettings.InitializationTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _ctx.AppCancellation);
        var ct = linkedCts.Token;
        var retrievalErrorCount = 0;

        while (!_ctx.AppCancellation.IsCancellationRequested)
        {
            try
            {
                var subIds = await _storage.RestoreSubscriberIDsForEventTypeAsync(
                                 new()
                                 {
                                     CancellationToken = ct,
                                     EventType = _tEvent.FullName!,
                                     Match = e => e.EventType == _tEvent.FullName! && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn,
                                     Projection = e => e.SubscriberID
                                 });

                foreach (var subId in subIds)
                    _registry.RestoreSubscriber(subId);

                return;
            }
            catch (Exception e)
            {
                if (timeoutCts.Token.IsCancellationRequested)
                {
                    //timeout reached. app shouldn't be allowed to start! (due to risk of losing events)
                    //https://discord.com/channels/933662816458645504/1335898618468634624/1336002378973057054
                    throw new ApplicationException($"Unable to restore subscribers for event type [{_tEvent.FullName!}] via storage provider in a timely manner!");
                }

                await _ctx.InvokeExceptionReceiverSafely(() => _ctx.Errors?.OnRestoreSubscriberIDsError(_tEvent, retrievalErrorCount++, e, ct));
                _ctx.Logger.RestoreSubscriberIDsError(_tEvent.FullName!);

                if (!_ctx.AppCancellation.IsCancellationRequested)
                    await Task.Delay(EventHubSettings.StorageRetryDelay, CancellationToken.None);
            }
        }
    }

    static readonly string[] _httPost = ["POST"];

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent, TStorageRecord, TStorageProvider>> ctx)
    {
        // ReSharper disable once UseSymbolAlias
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
    internal Task OnSubscriberConnected(EventHub<TEvent, TStorageRecord, TStorageProvider> _, string subscriberID, IServerStreamWriter<TEvent> stream, ServerCallContext ctx)
    {
        _ctx.Logger.SubscriberConnected(subscriberID, _tEvent.FullName!);

        return EventDispatcherWorker.RunAsync<TEvent, TStorageRecord, TStorageProvider>(
            _storage!,
            _storageBehavior,
            _registry,
            _ctx,
            subscriberID,
            stream,
            ctx.CancellationToken);
    }

    //WARNING: this method is never awaited. so it should not surface any exceptions.
    protected override async Task BroadcastEventTask(IEvent evnt)
    {
        _testEventReceiver?.AddEvent((TEvent)evnt);

        var subscribers = _selector.SelectRecipients(_registry);
        var startTime = DateTime.UtcNow;

        while (subscribers.Length == 0)
        {
            if (_ctx.AppCancellation.IsCancellationRequested || DateTime.UtcNow - startTime >= EventHubSettings.NoSubscriberTimeout)
                return;

            _ctx.Logger.NoSubscribersWarning(_tEvent.FullName!);
            await Task.Delay(EventHubSettings.NoSubscriberRetryDelay, CancellationToken.None);

            subscribers = _selector.SelectRecipients(_registry);
        }

        var records = CreateStorageRecords(subscribers, (TEvent)evnt);

        if (records is null)
            return; //serialization error

        await StoreEventRecords(records);
        SignalSubscribers(subscribers);
    }

    List<TStorageRecord>? CreateStorageRecords(string[] subscribers, TEvent evnt)
    {
        var records = new List<TStorageRecord>(subscribers.Length);

        foreach (var subId in subscribers)
        {
            var record = new TStorageRecord
            {
                SubscriberID = subId,
                TrackingID = Guid.NewGuid(),
                EventType = _tEvent.FullName!,
                ExpireOn = DateTime.UtcNow.Add(EventHubSettings.EventExpiry)
            };

            try
            {
                record.SetEvent(evnt);
            }
            catch (Exception ex)
            {
                _ = _ctx.InvokeExceptionReceiverSafely(() => _ctx.Errors?.OnSerializeEventError(evnt, ex, _ctx.AppCancellation));
                _ctx.Logger.SetEventCritical(_tEvent.FullName!, ex.Message);

                return null; //no point in continuing for other subscribers.
            }

            records.Add(record);
        }

        return records;
    }

    async Task StoreEventRecords(List<TStorageRecord> records)
    {
        var createErrorCount = 0;

        while (true)
        {
            try
            {
                // durable providers must persist the outgoing fan-out records even during shutdown so published
                // events remain available for delivery after restart.
                await _storage!.StoreEventsAsync(records, _storageBehavior.GetStoreEventsToken(_ctx.AppCancellation));
                createErrorCount = 0;

                break;
            }
            catch (OverflowException) when (IsInMemoryProvider)
            {
                foreach (var rec in records.Cast<InMemoryEventStorageRecord>().Where(r => r.QueueOverflowed))
                {
                    _registry.Remove(rec.SubscriberID, allowConfiguredRemoval: false);
                    _ = _ctx.InvokeExceptionReceiverSafely(() => _ctx.Errors?.OnInMemoryQueueOverflow<TEvent>(rec, _ctx.AppCancellation));
                    _ctx.Logger.QueueOverflowWarning(rec.SubscriberID, _tEvent.FullName!);
                }

                break;
            }
            catch (Exception ex)
            {
                await _ctx.InvokeExceptionReceiverSafely(() => _ctx.Errors?.OnStoreEventRecordsError<TEvent>(records, createErrorCount++, ex, _ctx.AppCancellation));
                _ctx.Logger.StoreEventsError(_tEvent.FullName!, ex.Message);

                if (_ctx.AppCancellation.IsCancellationRequested)
                    break;

                await Task.Delay(EventHubSettings.StorageRetryDelay, CancellationToken.None);
            }
        }
    }

    void SignalSubscribers(string[] subscribers)
    {
        foreach (var sid in subscribers)
            _registry.SignalSubscriber(sid);
    }

    internal Task BroadcastEventTaskForTesting(TEvent evnt)
        => BroadcastEventTask(evnt);

    //internal to allow unit testing
    internal string GetNextRoundRobinSubscriberId(string[] connectedSubIds)
        => ((RoundRobinSelector)_selector).GetNextRoundRobinSubscriberId(connectedSubIds);

    //internal to allow unit testing
    internal Task<EmptyObject> OnEventReceived(EventHub<TEvent, TStorageRecord, TStorageProvider> _, TEvent evnt, ServerCallContext __)
    {
        AddToSubscriberQueues(evnt);

        return Task.FromResult(EmptyObject.Instance);
    }
}