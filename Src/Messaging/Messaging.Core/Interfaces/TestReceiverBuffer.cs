namespace FastEndpoints;

// Shared storage and wait loop for the test event and command receivers.
sealed class TestReceiverBuffer<T>
{
    readonly List<T> _received = [];

    internal void Add(T item)
    {
        lock (_received)
            _received.Add(item);
    }

    internal async Task<IEnumerable<T>> WaitForMatchAsync(Func<T, bool> match, int timeoutSeconds, CancellationToken ct)
    {
        var start = DateTime.Now;

        while (!ct.IsCancellationRequested && DateTime.Now.Subtract(start).TotalSeconds < timeoutSeconds)
        {
            // Publishers and handlers can add concurrently, so match against a snapshot and return a materialized result.
            T[] snapshot;
            lock (_received)
                snapshot = [.. _received];

            var res = snapshot.Where(match).ToArray();

            if (res.Length > 0)
                return res;

            await Task.Delay(100, CancellationToken.None);
        }

        return [];
    }
}
