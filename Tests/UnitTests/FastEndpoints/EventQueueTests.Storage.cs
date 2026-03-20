using FastEndpoints;
using Xunit;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task subscriber_storage_dequeues_events_in_enqueue_order()
    {
        const string firstSubscriberId = "sub1";
        const string secondSubscriberId = "sub2";
        var eventType = typeof(string).FullName!;
        var storage = new InMemoryEventSubscriberStorage();
        var firstRecord = CreateInMemoryRecord(firstSubscriberId, "test1", eventType);
        var secondRecord = CreateInMemoryRecord(firstSubscriberId, "test2", eventType);
        var thirdRecord = CreateInMemoryRecord(secondSubscriberId, "test3", eventType);

        await storage.StoreEventAsync(firstRecord, CancellationToken.None);
        await storage.StoreEventAsync(secondRecord, CancellationToken.None);

        var firstBatch = await storage.GetNextBatchAsync(new() { SubscriberID = firstSubscriberId, EventType = eventType });
        firstBatch.Single().Event.ShouldBe(firstRecord.Event);

        var secondBatch = await storage.GetNextBatchAsync(new() { SubscriberID = firstSubscriberId, EventType = eventType });
        secondBatch.Single().Event.ShouldBe(secondRecord.Event);

        await storage.StoreEventAsync(thirdRecord, CancellationToken.None);
        await Task.Delay(100);

        var thirdBatch = await storage.GetNextBatchAsync(new() { SubscriberID = secondSubscriberId, EventType = eventType });
        thirdBatch.Single().Event.ShouldBe(thirdRecord.Event);

        var firstSubscriberRemainder = await storage.GetNextBatchAsync(new() { SubscriberID = firstSubscriberId, EventType = eventType });
        firstSubscriberRemainder.Any().ShouldBeFalse();

        var secondSubscriberRemainder = await storage.GetNextBatchAsync(new() { SubscriberID = secondSubscriberId, EventType = eventType });
        secondSubscriberRemainder.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task subscriber_storage_isolates_reused_explicit_ids_by_event_type()
    {
        const string subscriberId = "shared-sub";
        var storage = new InMemoryEventSubscriberStorage();

        await storage.StoreEventAsync(CreateInMemoryRecord(subscriberId, new KnownSubscriberEvent { EventID = 111 }), CancellationToken.None);
        await storage.StoreEventAsync(CreateInMemoryRecord(subscriberId, new ConfiguredSubscriberEvent { EventID = 222 }), CancellationToken.None);

        var knownBatch = await storage.GetNextBatchAsync(new() { SubscriberID = subscriberId, EventType = typeof(KnownSubscriberEvent).FullName! });
        var configuredBatch = await storage.GetNextBatchAsync(new() { SubscriberID = subscriberId, EventType = typeof(ConfiguredSubscriberEvent).FullName! });

        ((KnownSubscriberEvent)knownBatch.Single().Event).EventID.ShouldBe(111);
        ((ConfiguredSubscriberEvent)configuredBatch.Single().Event).EventID.ShouldBe(222);
    }

    [Fact]
    public async Task subscriber_storage_throws_when_the_queue_limit_is_exceeded()
    {
        var previousMaxLimit = InMemoryEventQueue.MaxLimit;
        var storage = new InMemoryEventSubscriberStorage();

        InMemoryEventQueue.MaxLimit = 10000;

        try
        {
            for (var index = 0; index <= InMemoryEventQueue.MaxLimit; index++)
            {
                var record = CreateInMemoryRecord("subId", index, typeof(string).FullName!);

                if (index < InMemoryEventQueue.MaxLimit)
                {
                    await storage.StoreEventAsync(record, CancellationToken.None);
                    continue;
                }

                var storeOverflow = async () => await storage.StoreEventAsync(record, CancellationToken.None);
                await storeOverflow.ShouldThrowAsync<OverflowException>();
            }
        }
        finally
        {
            InMemoryEventQueue.MaxLimit = previousMaxLimit;
        }
    }
}
