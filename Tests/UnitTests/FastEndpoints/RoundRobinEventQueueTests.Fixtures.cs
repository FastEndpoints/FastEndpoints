using FastEndpoints;

namespace EventQueue;

public partial class RoundRobinEventQueueTests
{
    interface IRoundRobinTestEvent : IEvent
    {
        int EventID { get; set; }
    }

    class RrTestEventOnlyOne : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrTestEventMulti : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrTestEventOneConnected : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrKnownSubscriberEvent : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrConcurrentSelectionEvent : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrConcurrentPublishEvent : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    class RrTestEventThreeSubs : IRoundRobinTestEvent
    {
        public int EventID { get; set; }
    }

    sealed class RoundRobinRecordingRecord : IEventStorageRecord
    {
        public string SubscriberID { get; set; } = default!;
        public Guid TrackingID { get; set; }
        public object Event { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class RoundRobinRecordingStorageState
    {
        readonly Lock _lock = new();
        readonly List<RoundRobinRecordingRecord> _records = [];

        public void Store(IEnumerable<RoundRobinRecordingRecord> records)
        {
            lock (_lock)
                _records.AddRange(records.Select(Clone));
        }

        public IReadOnlyList<RoundRobinRecordingRecord> GetStoredRecords()
        {
            lock (_lock)
                return _records.ToArray();
        }

        static RoundRobinRecordingRecord Clone(RoundRobinRecordingRecord record)
            => new()
            {
                SubscriberID = record.SubscriberID,
                TrackingID = record.TrackingID,
                Event = record.Event,
                EventType = record.EventType,
                ExpireOn = record.ExpireOn,
                IsComplete = record.IsComplete
            };
    }

    sealed class RoundRobinRecordingStorage(RoundRobinRecordingStorageState state) : IEventHubStorageProvider<RoundRobinRecordingRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<RoundRobinRecordingRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<RoundRobinRecordingRecord> records, CancellationToken ct)
        {
            state.Store(records);

            return default;
        }

        public ValueTask<IEnumerable<RoundRobinRecordingRecord>> GetNextBatchAsync(PendingRecordSearchParams<RoundRobinRecordingRecord> parameters)
            => new(Array.Empty<RoundRobinRecordingRecord>());

        public ValueTask MarkEventAsCompleteAsync(RoundRobinRecordingRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<RoundRobinRecordingRecord> parameters)
            => default;
    }
}