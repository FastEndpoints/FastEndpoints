// ReSharper disable MethodSupportsCancellation

using System.Collections.Concurrent;
using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

sealed class EventHubInitializer : IHostedService
{
    //ct passed in here is useless. it's a default token: https://source.dot.net/#Microsoft.AspNetCore.Hosting/Internal/WebHost.cs,124
    public async Task StartAsync(CancellationToken _)
        => await EventHubBase.InitializeHubs();

    public Task StopAsync(CancellationToken _)
        => Task.CompletedTask;
}

abstract class EventHubBase
{
    //key: tEvent
    //val: event hub for the event type
    //values get created when the DI container resolves each event hub type and the ctor is run.
    protected static readonly ConcurrentDictionary<Type, EventHubBase> AllHubs = new();

    protected bool IsInMemoryProvider { get; init; }

    protected abstract Task Initialize();

    protected abstract Task BroadcastEventTask(IEvent evnt);

    internal static Task InitializeHubs()
        => Task.WhenAll(AllHubs.Values.Where(hub => !hub.IsInMemoryProvider).Select(hub => hub.Initialize()));

    internal static void AddToSubscriberQueues(IEvent evnt)
    {
        var tEvent = evnt.GetType();

        if (AllHubs.TryGetValue(tEvent, out var hub))
            _ = hub.BroadcastEventTask(evnt); //executed in background. will never throw exceptions.
        else
            throw new InvalidOperationException($"An event hub has not been registered for [{tEvent.FullName}]");
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
    static readonly Lock _lock = new();
    static readonly Lock _configLock = new();
    static readonly Type _tEvent = typeof(TEvent);
    static readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();
    static readonly TimeSpan _subscriberRetention = TimeSpan.FromHours(24);
    static HashSet<string> _knownSubscriberIDs = [];
    static TStorageProvider? _storage;
    static bool _isRoundRobinMode;

    string? _lastReceivedBy;
    readonly EventHubExceptionReceiver? _errors;
    readonly ILogger _logger;
    readonly CancellationToken _appCancellation;
    readonly IEventReceiver<TEvent>? _testEventReceiver;

    internal static void Configure(HubMode mode, IEnumerable<string>? knownSubscriberIDs = null)
    {
        HashSet<string> configuredSubscriberIDs = [..knownSubscriberIDs?.Select(SubscriberIDFactory.Normalize).Distinct(StringComparer.Ordinal) ?? []];

        if (mode.HasFlag(HubMode.RoundRobin) && configuredSubscriberIDs.Count > 0)
        {
            throw new InvalidOperationException(
                $"Known subscriber IDs are not supported for round-robin event hub [{_tEvent.FullName!}]. Round-robin hubs only dispatch to currently connected subscribers.");
        }

        lock (_configLock)
        {
            Mode = mode;
            _isRoundRobinMode = mode.HasFlag(HubMode.RoundRobin);
            _knownSubscriberIDs = configuredSubscriberIDs;

            foreach (var subId in _knownSubscriberIDs)
                _subscribers.AddOrUpdate(subId, _ => new() { IsKnownSubscriber = true }, (_, existing) => existing with { IsKnownSubscriber = true });

            // if a subscriber was previously part of the configured set but is no longer listed, downgrade it to a
            // normal subscriber so stale config doesn't keep it pinned in the protected configured state forever.
            foreach (var kv in _subscribers.Where(kv => !_knownSubscriberIDs.Contains(kv.Key) && kv.Value.IsKnownSubscriber).ToArray())
            {
                while (_subscribers.TryGetValue(kv.Key, out var current) && current.IsKnownSubscriber)
                {
                    if (_subscribers.TryUpdate(kv.Key, current with { IsKnownSubscriber = false }, current))
                        break;
                }
            }
        }
    }

    public EventHub(IServiceProvider svcProvider)
    {
        AllHubs[_tEvent] = this;
        _isRoundRobinMode = Mode.HasFlag(HubMode.RoundRobin);
        _storage ??= (TStorageProvider)ActivatorUtilities.CreateInstance(svcProvider, typeof(TStorageProvider));
        IsInMemoryProvider = _storage is InMemoryEventHubStorage;
        EventHubStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //for stale record purging task setup
        EventHubStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = IsInMemoryProvider;
        _errors = svcProvider.GetService<EventHubExceptionReceiver>();
        _logger = svcProvider.GetRequiredService<ILogger<EventHub<TEvent, TStorageRecord, TStorageProvider>>>();
        _appCancellation = svcProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        _testEventReceiver = svcProvider.GetService<IEventReceiver<TEvent>>();
    }

    protected override async Task Initialize()
    {
        ArgumentNullException.ThrowIfNull(_storage);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _appCancellation);
        var ct = linkedCts.Token;
        var retrievalErrorCount = 0;

        while (!_appCancellation.IsCancellationRequested)
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
                {
                    _subscribers.AddOrUpdate(
                        subId,
                        _ => new() { LastSeenUtc = DateTime.UtcNow, IsKnownSubscriber = _knownSubscriberIDs.Contains(subId) },
                        (_, existing) => existing with { LastSeenUtc = DateTime.UtcNow, IsKnownSubscriber = existing.IsKnownSubscriber || _knownSubscriberIDs.Contains(subId) });
                }

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

                _errors?.OnRestoreSubscriberIDsError(_tEvent, retrievalErrorCount++, e, ct);
                _logger.RestoreSubscriberIDsError(_tEvent.FullName!);

                if (!_appCancellation.IsCancellationRequested)
                    await Task.Delay(5000, CancellationToken.None);
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
    internal async Task OnSubscriberConnected(EventHub<TEvent, TStorageRecord, TStorageProvider> _, string subscriberID, IServerStreamWriter<TEvent> stream, ServerCallContext ctx)
    {
        _logger.SubscriberConnected(subscriberID, _tEvent.FullName!);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, _appCancellation);
        SemaphoreSlim? subscriberSem = null;

        try
        {
            var retrievalErrorCount = 0;
            var updateErrorCount = 0;
            var subscriber = _subscribers.AddOrUpdate(
                subscriberID,
                _ => new()
                {
                    IsConnected = true, LastSeenUtc = DateTime.UtcNow, IsKnownSubscriber = _knownSubscriberIDs.Contains(subscriberID)
                },
                (_, existing) => existing with
                {
                    IsConnected = true, LastSeenUtc = DateTime.UtcNow, IsKnownSubscriber = existing.IsKnownSubscriber || _knownSubscriberIDs.Contains(subscriberID)
                });
            subscriberSem = subscriber.Sem;

            while (!cts.Token.IsCancellationRequested)
            {
                List<TStorageRecord> records;

                try
                {
                    records = (await _storage!.GetNextBatchAsync(
                                   new()
                                   {
                                       CancellationToken = cts.Token,
                                       EventType = _tEvent.FullName!,
                                       Limit = 25,
                                       SubscriberID = subscriberID,
                                       Match = e => e.SubscriberID == subscriberID &&
                                                    e.EventType == _tEvent.FullName! &&
                                                    !e.IsComplete &&
                                                    DateTime.UtcNow <= e.ExpireOn
                                   })).ToList();
                    retrievalErrorCount = 0;
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    retrievalErrorCount++;
                    _errors?.OnGetNextBatchError<TEvent>(subscriberID, retrievalErrorCount, ex, cts.Token);
                    _logger.StorageGetNextBatchError(subscriberID, _tEvent.FullName!, ex.Message);

                    if (!cts.Token.IsCancellationRequested)
                        await Task.Delay(5000);

                    continue;
                }

                if (records.Count > 0)
                {
                    for (var i = 0; i < records.Count; i++)
                    {
                        var record = records[i];

                        try
                        {
                            await stream.WriteAsync(record.GetEvent<TEvent>(), cts.Token);
                        }
                        catch
                        {
                            if (IsInMemoryProvider)
                            {
                                // re-queue the current record and all remaining unattempted records in the batch
                                // since they were already dequeued from the in-memory queue by GetNextBatchAsync.
                                try
                                {
                                    await _storage.StoreEventsAsync(records[i..], cts.Token);
                                }
                                catch
                                {
                                    //it's either canceled or queue is full
                                    //ignore and discard event if queue is full
                                }
                            }

                            UpdateSubscriber(subscriberID, s => s with { IsConnected = false, LastSeenUtc = DateTime.UtcNow });

                            return; //stream is most likely broken/canceled. exit the method here and let the subscriber re-connect and re-enter the method.
                        }

                        while (!IsInMemoryProvider)
                        {
                            try
                            {
                                record.IsComplete = true;

                                // use composite cancellation token (signals client canceled or app shutdown) here so an interrupted "post write ack" leaves the record pending
                                // for "at least once" redelivery if the client did not durably persist the event yet.
                                await _storage.MarkEventAsCompleteAsync(record, cts.Token);
                                updateErrorCount = 0;

                                break;
                            }
                            catch (Exception ex)
                            {
                                _errors?.OnMarkEventAsCompleteError<TEvent>(record, updateErrorCount++, ex, cts.Token);
                                _logger.StorageMarkAsCompleteError(subscriberID, _tEvent.FullName!, ex.Message);

                                if (cts.Token.IsCancellationRequested)
                                    break;

                                await Task.Delay(5000);
                            }
                        }
                    }
                }
                else
                {
                    await WaitForSignalAsync();
                }
            }

            //mark subscriber as disconnected if the while loop is exited.
            //which means the subscriber either canceled or stream got broken.
            UpdateSubscriber(subscriberID, s => s with { IsConnected = false, LastSeenUtc = DateTime.UtcNow });
        }
        finally
        {
            cts.Dispose();
        }

        async Task WaitForSignalAsync()
        {
            try
            {
                if (await subscriberSem!.WaitAsync(TimeSpan.FromSeconds(10), cts.Token)) //wait for poll interval, semaphore release, or shutdown.
                    while (subscriberSem.Wait(0)) { }                                      //drain residual releases so the next poll only runs after new work arrives.
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                //don't throw. let the main loop exit naturally so the disconnect state is updated.
            }
        }
    }

    //WARNING: this method is never awaited. so it should not surface any exceptions.
    protected override async Task BroadcastEventTask(IEvent evnt)
    {
        _testEventReceiver?.AddEvent((TEvent)evnt);

        var subscribers = GetReceiveCandidates();
        var startTime = DateTime.UtcNow;

        while (subscribers.Length == 0)
        {
            if (_appCancellation.IsCancellationRequested || (DateTime.UtcNow - startTime).TotalSeconds >= 60)
                return;

            _logger.NoSubscribersWarning(_tEvent.FullName!);
            await Task.Delay(5000, CancellationToken.None);

            subscribers = GetReceiveCandidates();
        }

        var records = new List<TStorageRecord>(subscribers.Length);

        foreach (var subId in subscribers)
        {
            var record = new TStorageRecord
            {
                SubscriberID = subId,
                TrackingID = Guid.NewGuid(),
                EventType = _tEvent.FullName!,
                ExpireOn = DateTime.UtcNow.AddHours(4)
            };

            try
            {
                record.SetEvent((TEvent)evnt);
            }
            catch (Exception ex)
            {
                _errors?.OnSerializeEventError(evnt, ex, _appCancellation);
                _logger.SetEventCritical(_tEvent.FullName!, ex.Message);

                return; //no point in continuing for other subscribers.
            }

            records.Add(record);
        }

        var createErrorCount = 0;

        while (true)
        {
            try
            {
                // durable providers must persist the outgoing fan-out records even during shutdown so published
                // events remain available for delivery after restart.
                await _storage!.StoreEventsAsync(records, IsInMemoryProvider ? _appCancellation : CancellationToken.None);
                createErrorCount = 0;

                break;
            }
            catch (OverflowException) when (IsInMemoryProvider)
            {
                foreach (var rec in records.Cast<InMemoryEventStorageRecord>().Where(r => r.QueueOverflowed))
                {
                    RemoveSubscriber(rec.SubscriberID, allowConfiguredRemoval: false);
                    _errors?.OnInMemoryQueueOverflow<TEvent>(rec, _appCancellation);
                    _logger.QueueOverflowWarning(rec.SubscriberID, _tEvent.FullName!);
                }

                break;
            }
            catch (Exception ex)
            {
                _errors?.OnStoreEventRecordsError<TEvent>(records, createErrorCount++, ex, _appCancellation);
                _logger.StoreEventsError(_tEvent.FullName!, ex.Message);

                if (_appCancellation.IsCancellationRequested)
                    break;

                await Task.Delay(5000, CancellationToken.None);
            }
        }

        foreach (var sid in subscribers)
        {
            if (!_subscribers.TryGetValue(sid, out var subscriber))
                continue;

            try
            {
                subscriber.Sem.Release();
            }
            catch (ObjectDisposedException)
            {
                //subscriber was removed after persistence completed. event will be picked up on reconnect if needed.
            }
        }

        string[] GetReceiveCandidates()
        {
            var staleCutoff = DateTime.UtcNow.Subtract(_subscriberRetention);

            foreach (var kv in _subscribers.Where(kv => kv.Value is { IsKnownSubscriber: false, IsConnected: false } && kv.Value.LastSeenUtc <= staleCutoff).ToArray())
            {
                //remove only if the entry is still the same stale snapshot we inspected above.
                //this avoids pruning a subscriber that reconnected or was otherwise updated after the snapshot was taken.
                if (_subscribers.TryRemove(new(kv.Key, kv.Value)))
                    kv.Value.Sem.Dispose();
            }

            if (!_isRoundRobinMode)
                return _subscribers.Keys.ToArray();

            var connectedSubIds = _subscribers
                                  .Where(kv => kv.Value.IsConnected)
                                  .Select(kv => kv.Key)
                                  .ToArray(); //take a snapshot of currently connected subscriber ids

            if (connectedSubIds.Length <= 1)
                return connectedSubIds;

            lock (_lock)
            {
                var lastIndex = Array.IndexOf(connectedSubIds, _lastReceivedBy);
                var nextIndex = (lastIndex + 1) % connectedSubIds.Length;
                _lastReceivedBy = connectedSubIds[nextIndex];
            }

            return [_lastReceivedBy];
        }
    }

    internal Task BroadcastEventTaskForTesting(TEvent evnt)
        => BroadcastEventTask(evnt);

    //internal to allow unit testing
    internal Task<EmptyObject> OnEventReceived(EventHub<TEvent, TStorageRecord, TStorageProvider> _, TEvent evnt, ServerCallContext __)
    {
        AddToSubscriberQueues(evnt);

        return Task.FromResult(EmptyObject.Instance);
    }

    static Subscriber UpdateSubscriber(string subscriberID, Func<Subscriber, Subscriber> update)
    {
        while (true)
        {
            if (!_subscribers.TryGetValue(subscriberID, out var current))
                return _subscribers.GetOrAdd(subscriberID, _ => update(new()));

            var updated = update(current);

            if (_subscribers.TryUpdate(subscriberID, updated, current))
                return updated;
        }
    }

    static void RemoveSubscriber(string subscriberID, bool allowConfiguredRemoval)
    {
        while (_subscribers.TryGetValue(subscriberID, out var current))
        {
            if (current.IsKnownSubscriber && !allowConfiguredRemoval)
                return;

            if (!_subscribers.TryRemove(new(subscriberID, current)))
                continue;

            current.Sem.Dispose();

            return;
        }
    }

    record Subscriber
    {
        public SemaphoreSlim Sem { get; } = new(0); //semaphorslim for waiting on record availability
        public bool IsConnected { get; init; }
        public DateTime LastSeenUtc { get; init; } = DateTime.UtcNow;
        public bool IsKnownSubscriber { get; init; }
    }
}
