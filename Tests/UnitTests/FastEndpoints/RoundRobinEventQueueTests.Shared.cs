using System.Reflection;
using FastEndpoints;
using QueueTesting;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class RoundRobinEventQueueTests
{
    static Task ConnectAnonymousSubscriber<TEvent>(EventHub<TEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage> hub,
                                                   TestServerStreamWriter<TEvent> writer,
                                                   CancellationToken cancellationToken) where TEvent : class, IEvent
        => hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, CreateServerCallContext(cancellationToken));

    static Task ConnectSubscriber<TEvent, TStorageRecord, TStorageProvider>(EventHub<TEvent, TStorageRecord, TStorageProvider> hub,
                                                                            string subscriberId,
                                                                            TestServerStreamWriter<TEvent> writer,
                                                                            CancellationToken cancellationToken)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
        => hub.OnSubscriberConnected(hub, subscriberId, writer, CreateServerCallContext(cancellationToken));

    static void PublishEvents<TEvent>(params int[] eventIds) where TEvent : class, IRoundRobinTestEvent, new()
    {
        foreach (var eventId in eventIds)
            EventHubBase.AddToSubscriberQueues(new TEvent { EventID = eventId });
    }

    static int TotalResponses<TEvent>(params TestServerStreamWriter<TEvent>[] writers)
        => writers.Sum(writer => writer.Responses.Count);

    static Task WaitForTotalResponses<TEvent>(int expectedTotal, params TestServerStreamWriter<TEvent>[] writers)
        => WaitUntil(() => TotalResponses(writers) == expectedTotal);

    static int[] GetEventIds<TEvent>(TestServerStreamWriter<TEvent> writer) where TEvent : class, IRoundRobinTestEvent
        => writer.Responses.Select(response => response.EventID).ToArray();

    static void AssertExactlyOneWriterReceived<TEvent>(int[] expectedEventIds, params TestServerStreamWriter<TEvent>[] writers)
        where TEvent : class, IRoundRobinTestEvent
    {
        var nonEmptyWriters = writers.Where(writer => writer.Responses.Count > 0).ToArray();

        nonEmptyWriters.Length.ShouldBe(1);
        GetEventIds(nonEmptyWriters[0]).ShouldBe(expectedEventIds);

        foreach (var writer in writers.Except(nonEmptyWriters))
            writer.Responses.ShouldBeEmpty();
    }

    static bool SubscriberExists<TEvent>(string subscriberId) where TEvent : class, IEvent
        => TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out _);

    static bool SubscriberExists<TEvent, TStorageRecord, TStorageProvider>(string subscriberId)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
        => TryGetSubscriberByHubType(typeof(EventHub<TEvent, TStorageRecord, TStorageProvider>), subscriberId, out _);

    static bool TryGetInMemorySubscriber(Type eventType, string subscriberId, out object? subscriber)
    {
        var hubType = typeof(EventHub<,,>).MakeGenericType(eventType, typeof(InMemoryEventStorageRecord), typeof(InMemoryEventHubStorage));

        return TryGetSubscriberByHubType(hubType, subscriberId, out subscriber);
    }

    static bool TryGetSubscriberByHubType(Type hubType, string subscriberId, out object? subscriber)
    {
        var field = hubType.GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dictionary = field.GetValue(null)!;
        var args = new object?[] { subscriberId, null };
        var found = (bool)dictionary.GetType().GetMethod("TryGetValue")!.Invoke(dictionary, args)!;
        subscriber = args[1];

        return found;
    }
}
