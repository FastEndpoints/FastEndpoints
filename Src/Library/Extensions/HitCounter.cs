using System.Collections.Concurrent;

namespace FastEndpoints;

internal class HitCounter
{
    //key: header value (specified by user in endpoint config)
    private readonly ConcurrentDictionary<string, Counter> dic = new();
    private readonly double _durationMillis;
    private readonly int _limit;

    internal string HeaderName { get; }

    internal HitCounter(string headerName, int durationSeconds, int hitLimit)
    {
        HeaderName = headerName;
        _durationMillis = durationSeconds * 1000;
        _limit = hitLimit;
    }

    internal bool LimitReached(string headerValue)
    {
        var counter = dic.GetOrAdd(headerValue, new Counter(_limit, _durationMillis, headerValue, dic));
        counter.Increase();
        return counter.LimitReached;
    }

    private class Counter : IDisposable
    {
        private int _count;
        private readonly string _key;
        private readonly int _limit;
        private ConcurrentDictionary<string, Counter>? _dictionary;
        private System.Timers.Timer? _timer;

        internal bool LimitReached => _count >= _limit;

        internal Counter(int limit, double durationMillis, string key, ConcurrentDictionary<string, Counter> dictionary)
        {
            _limit = limit;
            _key = key;
            _dictionary = dictionary;
            _timer = new(durationMillis) { AutoReset = false };
            _timer.Elapsed += RemoveCounter;
            _timer.Start();
        }

        private void RemoveCounter(object? _, System.Timers.ElapsedEventArgs __)
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