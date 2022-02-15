using System.Collections.Concurrent;

namespace FastEndpoints;

internal class HitCounter
{
    //key: header value as a unique client identifier
    private readonly ConcurrentDictionary<string, Counter> clients = new();
    private readonly double _durationMillis;
    private readonly int _limit;

    internal string? HeaderName { get; }

    internal HitCounter(string? headerName, int durationSeconds, int hitLimit)
    {
        HeaderName = headerName;
        _durationMillis = durationSeconds * 1000;
        _limit = hitLimit;
    }

    internal bool LimitReached(string headerValue)
    {
        var counter = clients.GetOrAdd(headerValue, new Counter(_limit, _durationMillis, headerValue, clients));
        counter.Increase();
        return counter.LimitReached;
    }

    private class Counter : IDisposable
    {
        private int _count;
        private readonly string _key;
        private readonly int _limit;
        private ConcurrentDictionary<string, Counter>? _dictionary;
        private Timer? _timer;

        internal bool LimitReached => _count > _limit;

        internal Counter(int limit, double durationMillis, string key, ConcurrentDictionary<string, Counter> dictionary)
        {
            _limit = limit;
            _key = key;
            _dictionary = dictionary;
            _timer = new Timer(
                callback: RemoveCounter,
                state: null,
                dueTime: TimeSpan.FromSeconds(durationMillis),
                period: TimeSpan.FromSeconds(1)); //keep firing every second, in case dictionary removal doesn't work the first time
        }

        private void RemoveCounter(object? _)
        {
            if (_dictionary?.TryRemove(_key, out var counter) is true)
                counter.Dispose();
        }

        internal void Increase()
        {
            Interlocked.Increment(ref _count);
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _dictionary = null;
                    _timer?.Dispose();
                    _timer = null;
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}