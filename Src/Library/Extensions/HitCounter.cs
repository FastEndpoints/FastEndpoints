using System.Collections.Concurrent;

namespace FastEndpoints;

internal class HitCounter
{
    //key: header value as a unique client identifier
    private ConcurrentDictionary<string, Counter> clients = new();
    private readonly double _durationSeconds;
    private readonly int _limit;

    internal string? HeaderName { get; }

    internal HitCounter(string? headerName, double durationSeconds, int hitLimit)
    {
        HeaderName = headerName;
        _durationSeconds = durationSeconds;
        _limit = hitLimit;
    }

    internal bool LimitReached(string headerValue)
    {
        var counter = clients.GetOrAdd(headerValue, new Counter(_durationSeconds, headerValue, ref clients));
        if (counter.Count < _limit)
        {
            counter.Increase();
            Console.WriteLine("incresed: " + counter.GetHashCode().ToString() + " - " + counter.Count.ToString());
            return false;
        }
        return true;
    }

    private class Counter : IDisposable
    {
        private readonly string _key;
        private readonly ConcurrentDictionary<string, Counter>? _dictionary;
        private readonly System.Timers.Timer _timer;
        private int _count;

        internal int Count => _count;

        internal Counter(double durationSeconds, string key, ref ConcurrentDictionary<string, Counter> dictionary)
        {
            _key = key;
            _dictionary = dictionary;
            _timer = new(durationSeconds * 1000);
            _timer.AutoReset = false;
            _timer.Elapsed += RemoveCounter;
            _timer.Start();
        }

        private void RemoveCounter(object? _, System.Timers.ElapsedEventArgs __)
        {
            if (_dictionary?.TryRemove(_key, out var counter) is true)
            {
                Console.WriteLine("removed: " + counter.GetHashCode().ToString());
                counter.Dispose();
            }
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
                    _timer.Dispose();
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