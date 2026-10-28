using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task hub_non_empty_batch_stops_when_canceled_during_idle_wait(bool appCancellation)
    {
        using var connection = new CancellationTokenSource();
        using var app = new CancellationTokenSource();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(connection.Token, app.Token);
        using var semaphore = new SemaphoreSlim(0);
        var storage = new IdleStorage();
        var ctx = new HubContext(NullLogger.Instance, null, typeof(OrdinaryRetrievalEvent).FullName!, app.Token);
        var fetch = ctx.GetNextNonEmptyBatch<OrdinaryRetrievalEvent, RetrievalRecord<OrdinaryRetrievalEvent>, IdleStorage>(
            storage, "retrieval-sub", EventHubSettings.BatchSize, semaphore, cts);

        fetch.IsCompleted.ShouldBeFalse();
        (appCancellation ? app : connection).Cancel();

        (await fetch.WaitAsync(TimeSpan.FromSeconds(3))).ShouldBeNull();
        storage.Limits.Count.ShouldBe(1);
    }

    [Fact]
    public async Task hub_non_empty_batch_stops_when_subscriber_semaphore_is_disposed()
    {
        using var cts = new CancellationTokenSource();
        using var semaphore = new SemaphoreSlim(0);
        semaphore.Dispose();
        var storage = new IdleStorage();
        var ctx = new HubContext(NullLogger.Instance, null, typeof(OrdinaryRetrievalEvent).FullName!, CancellationToken.None);

        var records = await ctx.GetNextNonEmptyBatch<OrdinaryRetrievalEvent, RetrievalRecord<OrdinaryRetrievalEvent>, IdleStorage>(
                          storage, "retrieval-sub", 1, semaphore, cts).WaitAsync(TimeSpan.FromSeconds(3));

        records.ShouldBeNull();
        cts.IsCancellationRequested.ShouldBeTrue();
        storage.Limits.Count.ShouldBe(1);
    }

    [Fact]
    public async Task hub_non_empty_batch_fetches_again_after_signal_and_drains_residual_releases()
    {
        using var cts = new CancellationTokenSource();
        using var semaphore = new SemaphoreSlim(0);
        var record = new RetrievalRecord<OrdinaryRetrievalEvent>();
        var storage = new IdleStorage { NextBatch = [record] };
        var ctx = new HubContext(NullLogger.Instance, null, typeof(OrdinaryRetrievalEvent).FullName!, CancellationToken.None);
        var fetch = ctx.GetNextNonEmptyBatch<OrdinaryRetrievalEvent, RetrievalRecord<OrdinaryRetrievalEvent>, IdleStorage>(
            storage, "retrieval-sub", 1, semaphore, cts);

        fetch.IsCompleted.ShouldBeFalse();
        semaphore.Release(3);

        (await fetch.WaitAsync(TimeSpan.FromSeconds(3))).ShouldBe([record]);
        semaphore.CurrentCount.ShouldBe(0);
        storage.Limits.ShouldBe([1, 1]);
    }

    sealed class IdleStorage : IEventHubStorageProvider<RetrievalRecord<OrdinaryRetrievalEvent>>
    {
        internal readonly List<int> Limits = [];
        internal IEnumerable<RetrievalRecord<OrdinaryRetrievalEvent>> NextBatch = [];

        public ValueTask<IEnumerable<RetrievalRecord<OrdinaryRetrievalEvent>>> GetNextBatchAsync(PendingRecordSearchParams<RetrievalRecord<OrdinaryRetrievalEvent>> parameters)
        {
            Limits.Add(parameters.Limit);

            return new(Limits.Count == 1 ? [] : NextBatch);
        }

        public ValueTask MarkEventAsCompleteAsync(RetrievalRecord<OrdinaryRetrievalEvent> record, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<RetrievalRecord<OrdinaryRetrievalEvent>> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<RetrievalRecord<OrdinaryRetrievalEvent>> records, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<RetrievalRecord<OrdinaryRetrievalEvent>> parameters)
            => ValueTask.CompletedTask;
    }
}
