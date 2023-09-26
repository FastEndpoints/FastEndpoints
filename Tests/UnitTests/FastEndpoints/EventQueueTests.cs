using FakeItEasy;
using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EventQueue;

public class EventQueueTests
{
    [Fact]
    public async Task subscriber_storage_queue_and_dequeue()
    {
        var sut = new InMemoryEventSubscriberStorage();

        var record1 = new InMemoryEventStorageRecord
        {
            Event = "test1",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record2 = new InMemoryEventStorageRecord
        {
            Event = "test2",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record3 = new InMemoryEventStorageRecord
        {
            Event = "test3",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub2"
        };

        await sut.StoreEventAsync(record1, default);
        await sut.StoreEventAsync(record2, default);

        var r1x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID });
        r1x!.Single().Event.Should().Be(record1.Event);

        var r2x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID });
        r2x!.Single().Event.Should().Be(record2.Event);

        await sut.StoreEventAsync(record3, default);
        await Task.Delay(100);

        var r3 = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID });
        r3!.Single().Event.Should().Be(record3.Event);

        var r3x = await sut.GetNextBatchAsync(new() { SubscriberID = record2.SubscriberID });
        r3x.Any().Should().BeFalse();

        var r4x = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID });
        r4x.Any().Should().BeFalse();
    }

    [Fact]
    public async Task subscriber_storage_queue_overflow()
    {
        var sut = new InMemoryEventSubscriberStorage();

        InMemoryEventQueue.MaxLimit = 10000;

        for (var i = 0; i <= InMemoryEventQueue.MaxLimit; i++)
        {
            var r = new InMemoryEventStorageRecord
            {
                Event = i,
                EventType = "System.String",
                ExpireOn = DateTime.UtcNow.AddMinutes(1),
                SubscriberID = "sub1"
            };

            if (i < InMemoryEventQueue.MaxLimit)
            {
                await sut.StoreEventAsync(r, default);
            }
            else
            {
                var func = async () => await sut.StoreEventAsync(r, default);
                await func.Should().ThrowAsync<OverflowException>();
            }
        }
    }

    [Fact]
    public async Task event_hub_publisher_mode()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var writer = new TestServerStreamWriter<TestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, "sub1", writer, ctx);
        _ = hub.OnSubscriberConnected(hub, "sub2", writer, ctx);

        var e1 = new TestEvent { EventID = 123 };
        await EventHubBase.AddToSubscriberQueues(e1, default);
        await Task.Delay(100);

        writer.Responses[0].EventID.Should().Be(123);
    }

    [Fact]
    public async Task event_hub_publisher_mode_round_robin()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<RRTestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<RRTestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var writerA = new TestServerStreamWriter<RRTestEvent>();
        var writerB = new TestServerStreamWriter<RRTestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, "subA", writerA, ctx);
        _ = hub.OnSubscriberConnected(hub, "subB", writerB, ctx);

        await Task.Delay(500);

        var e1 = new RRTestEvent { EventID = 111 };
        await EventHubBase.AddToSubscriberQueues(e1, default);

        var e2 = new RRTestEvent { EventID = 222 };
        await EventHubBase.AddToSubscriberQueues(e2, default);

        var e3 = new RRTestEvent { EventID = 333 };
        await EventHubBase.AddToSubscriberQueues(e3, default);

        await Task.Delay(500);

        if (writerA.Responses.Count == 2)
        {
            writerB.Responses.Count.Should().Be(1);
            writerB.Responses[0].EventID.Should().Be(222);
            writerA.Responses[0].EventID.Should().Be(111);
            writerA.Responses[1].EventID.Should().Be(333);
        }
        else if (writerB.Responses.Count == 1)
        {
            writerA.Responses.Count.Should().Be(2);
            writerA.Responses[0].EventID.Should().Be(111);
            writerA.Responses[1].EventID.Should().Be(333);
            writerB.Responses[0].EventID.Should().Be(222);
        }
    }

    [Fact]
    public async Task event_hub_publisher_mode_round_robin_only_one_subscriber()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<RRTestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<RRTestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var writer = new TestServerStreamWriter<RRTestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, "subA", writer, ctx);

        await Task.Delay(500);

        var e1 = new RRTestEvent { EventID = 111 };
        await EventHubBase.AddToSubscriberQueues(e1, default);

        var e2 = new RRTestEvent { EventID = 222 };
        await EventHubBase.AddToSubscriberQueues(e2, default);

        var e3 = new RRTestEvent { EventID = 333 };
        await EventHubBase.AddToSubscriberQueues(e3, default);

        await Task.Delay(500);

        writer.Responses.Count.Should().Be(3);
        writer.Responses[0].EventID.Should().Be(111);
        writer.Responses[1].EventID.Should().Be(222);
        writer.Responses[2].EventID.Should().Be(333);
    }

    [Fact]
    public async Task event_hub_broker_mode()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventBroker;

        var writer = new TestServerStreamWriter<TestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, "event-sub-1", writer, ctx);

        var e1 = new TestEvent { EventID = 321 };
        _ = hub.OnEventReceived(hub, e1, ctx);
        await Task.Delay(100);

        writer.Responses[0].EventID.Should().Be(321);
    }

    private class TestEvent : IEvent
    {
        public int EventID { get; set; }
    }

    private class RRTestEvent : IRoundRobinEvent
    {
        public int EventID { get; set; }
    }

    private class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new List<T>();

        public async Task WriteAsync(T message)
            => Responses.Add(message);
    }
}