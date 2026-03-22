using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// Manages per-job CancellationTokenSource lifecycle and manual cancellation markers.
/// Cancellation tracking uses a null-sentinel convention:
/// - A non-null CTS entry means the job is active or pre-canceled (pre-canceled uses the shared static instance).
/// - A null entry means the job was manually canceled via CancelJobAsync.
/// Stale markers are enqueued when a job is manually canceled and cleaned up after a TTL,
/// provided the job is no longer in-flight.
/// </summary>
sealed class JobCancellationTracker
{
    static readonly CancellationTokenSource _preCancelledTokenSource;

    readonly ConcurrentDictionary<Guid, CancellationTokenSource?> _cancellations = new();
    readonly ConcurrentQueue<(Guid TrackingId, DateTime ExpireAt)> _staleMarkers = new();

    static JobCancellationTracker()
    {
        _preCancelledTokenSource = new();
        _preCancelledTokenSource.Cancel();
    }

    /// <summary>
    /// Gets or adds a CTS for a job. The ConcurrentDictionary race (factory called on multiple threads
    /// but only one result stored) is handled by disposing the losing CTS.
    /// Returns null if the entry was already set to null by <see cref="TryMarkForManualCancel" />.
    /// </summary>
    internal CancellationTokenSource? GetOrAdd(Guid trackingId, Func<CancellationTokenSource> factory)
    {
        CancellationTokenSource? created = null;

        var cts = _cancellations.GetOrAdd(
            trackingId,
            _ =>
            {
                created = factory();

                return created;
            });

        if (created is not null && !ReferenceEquals(created, cts))
            SafeDispose(created);

        return cts;
    }

    /// <summary>
    /// Adds a pre-canceled marker for a job that hasn't started executing yet.
    /// Returns the CTS found (existing or pre-canceled).
    /// Returns null if the job was already marked for manual cancellation.
    /// </summary>
    internal CancellationTokenSource? GetCancellationOrMarker(Guid trackingId)
        => GetOrAdd(trackingId, static () => _preCancelledTokenSource);

    /// <summary>
    /// Removes the tracking entry for a completed or failed job.
    /// </summary>
    internal void Remove(Guid trackingId)
        => _cancellations.TryRemove(trackingId, out _);

    /// <summary>
    /// Atomically marks a job for manual cancellation by setting its entry from the given CTS to null.
    /// Returns true if the marker was set; false if the entry was already removed, already null, or the CTS changed.
    /// On success, enqueues a stale marker for cleanup after 5 minutes.
    /// </summary>
    internal bool TryMarkForManualCancel(Guid trackingId, CancellationTokenSource cts)
    {
        if (!_cancellations.TryUpdate(trackingId, null, cts))
            return false;

        _staleMarkers.Enqueue((trackingId, DateTime.UtcNow.AddMinutes(5)));

        return true;
    }

    /// <summary>
    /// Returns true if the job was manually canceled (entry exists and is null).
    /// </summary>
    internal bool IsManuallyCancelled(Guid trackingId)
        => _cancellations.TryGetValue(trackingId, out var c) && c is null;

    /// <summary>
    /// Cleans up expired stale manual cancellation markers.
    /// The <paramref name="isInFlight" /> predicate should return true if the job is currently executing.
    /// </summary>
    internal void CleanupStaleMarkers(Func<Guid, bool> isInFlight)
    {
        var now = DateTime.UtcNow;

        while (_staleMarkers.TryPeek(out var item) && item.ExpireAt <= now)
        {
            if (!_staleMarkers.TryDequeue(out item))
                continue;

            if (_cancellations.TryGetValue(item.TrackingId, out var cts) && cts is null && !isInFlight(item.TrackingId))
                _cancellations.TryRemove(item.TrackingId, out _);
        }
    }

    /// <summary>
    /// Safely disposes a CTS, avoiding disposal of the shared pre-canceled instance.
    /// </summary>
    internal static void SafeDispose(CancellationTokenSource? cts)
    {
        if (cts is not null && !ReferenceEquals(cts, _preCancelledTokenSource))
            cts.Dispose();
    }
}