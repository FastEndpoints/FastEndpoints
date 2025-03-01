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
    static readonly ConcurrentDictionary<string, Subscriber> _subscribers = new();
    static readonly Type _tEvent = typeof(TEvent);
    static bool _isRoundRobinMode;
    static TStorageProvider? _storage;

    static readonly Lock _lock = new();

    string? _lastReceivedBy;
    readonly EventHubExceptionReceiver? _errors;
    readonly ILogger _logger;
    readonly CancellationToken _appCancellation;

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
    }

    protected override async Task Initialize()
    {
        ArgumentNullException.ThrowIfNull(_storage);

        var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        var ct = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, _appCancellation).Token;
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
                    _subscribers[subId] = new();

                return;
            }
            catch (Exception e)
            {
                if (timeoutToken.IsCancellationRequested)
                {
                    //timeout reached. app shouldn't be allowed to start! (due to risk of losing events)
                    //https://discord.com/channels/933662816458645504/1335898618468634624/1336002378973057054
                    throw new ApplicationException($"Unable to restore subscribers for event [{_tEvent.FullName!}] via storage provider in a timely manner!");
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
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, _appCancellation);

        while (!cts.Token.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await _storage!.GetNextBatchAsync(
                              new()
                              {
                                  CancellationToken = cts.Token,
                                  Limit = 25,
                                  SubscriberID = subscriberID,
                                  Match = e => e.SubscriberID == subscriberID && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn
                              });
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                _errors?.OnGetNextBatchError<TEvent>(subscriberID, retrievalErrorCount++, ex, cts.Token);
                _logger.StorageGetNextBatchError(subscriberID, _tEvent.FullName!, ex.Message);

                if (!cts.Token.IsCancellationRequested)
                    await Task.Delay(5000);

                continue;
            }

            if (records.Any())
            {
                foreach (var record in records)
                {
                    try
                    {
                        await stream.WriteAsync(record.GetEvent<TEvent>(), cts.Token);
                    }
                    catch
                    {
                        if (IsInMemoryProvider)
                        {
                            try
                            {
                                await _storage.StoreEventsAsync([record], cts.Token);
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

                    while (!IsInMemoryProvider)
                    {
                        try
                        {
                            record.IsComplete = true;
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
                //wait until either the semaphore is released or a minute has elapsed
                await Task.WhenAny(subscriber.Sem.WaitAsync(cts.Token), Task.Delay(60000));
            }
        }

        //mark subscriber as disconnected if the while loop is exited.
        //which means the subscriber either cancelled or stream got broken.
        if (_isRoundRobinMode)
            subscriber.IsConnected = false;
    }

    //WARNING: this method is never awaited. so it should not surface any exceptions.
    protected override async Task BroadcastEventTask(IEvent evnt)
    {
        var subscribers = GetReceiveCandidates();
        var startTime = DateTime.Now;

        while (subscribers.Length == 0)
        {
            if (_appCancellation.IsCancellationRequested || (DateTime.Now - startTime).TotalSeconds >= 60)
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
                await _storage!.StoreEventsAsync(records, _appCancellation);

                foreach (var sid in subscribers)
                    _subscribers[sid].Sem.Release();

                createErrorCount = 0;

                break;
            }
            catch (OverflowException)
            {
                foreach (var rec in records.Cast<InMemoryEventStorageRecord>().Where(r => r.QueueOverflowed))
                {
                    _subscribers.Remove(rec.SubscriberID, out var sub);
                    sub?.Sem.Dispose();
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

        string[] GetReceiveCandidates()
        {
            if (!_isRoundRobinMode)
                return _subscribers.Keys.ToArray();

            var connectedSubIds = _subscribers
                                  .Where(kv => kv.Value.IsConnected)
                                  .Select(kv => kv.Key)
                                  .ToArray(); //take a snapshot of currently connected subscriber ids

            if (connectedSubIds.Length <= 1)
                return connectedSubIds;

            string[] qualified;

            lock (_lock)
            {
                qualified = connectedSubIds.SkipWhile(s => s == _lastReceivedBy).Take(1).ToArray();
                _lastReceivedBy = qualified.Single();
            }

            return qualified;
        }
    }

    //internal to allow unit testing
    internal Task<EmptyObject> OnEventReceived(EventHub<TEvent, TStorageRecord, TStorageProvider> _, TEvent evnt, ServerCallContext __)
    {
        AddToSubscriberQueues(evnt);

        return Task.FromResult(EmptyObject.Instance);
    }

    class Subscriber
    {
        public SemaphoreSlim Sem { get; } = new(0); //semaphorslim for waiting on record availability
        public bool IsConnected { get; set; }
    }
}