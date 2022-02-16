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
        return clients.GetOrAdd(
            headerValue,
            hVal => new Counter(_durationSeconds, hVal, _limit, ref clients)
            ).LimitReached();
    }

    private class Counter : IDisposable
    {
        private readonly string _self;
        private readonly ConcurrentDictionary<string, Counter> _parent;
        private Timer? _timer;
        private long _startTicks;
        private readonly TimeSpan _duration;
        private readonly int _limit;
        private int _count;

        private bool Expired => DateTimeOffset.UtcNow.Subtract(_duration).Ticks > _startTicks;

        internal Counter(double durationSeconds, string key, int limit, ref ConcurrentDictionary<string, Counter> dictionary)
        {
            _startTicks = DateTimeOffset.UtcNow.Ticks;
            _duration = TimeSpan.FromSeconds(durationSeconds);
            _self = key;
            _limit = limit;
            _parent = dictionary;
            _timer = new(RemoveFromParentIfExpired, null, 60000, 60000);
        }

        internal bool LimitReached()
        {
            if (Expired)
            {
                Interlocked.Exchange(ref _startTicks, DateTimeOffset.UtcNow.Ticks);
                Interlocked.Exchange(ref _count, 0);
                //Console.WriteLine($"reset: {GetHashCode()}");
            }

            if (_count >= _limit)
            {
                //Console.WriteLine($"limit reached: {GetHashCode()}");
                return true;
            }

            //Console.WriteLine($"hit: {GetHashCode()}");
            Interlocked.Increment(ref _count);
            return false;
        }

        private void RemoveFromParentIfExpired(object? _)
        {
            //Console.WriteLine($"clean event: {GetHashCode()}");

            if (Expired && _parent.TryRemove(_self, out var counter))
            {
                Dispose();
                //Console.WriteLine($"disposed: {counter.GetHashCode()} / {GetHashCode()}");
            }
            //else if (Expired)
            //{
            //    Console.WriteLine($"failed cleanup: {GetHashCode()}");
            //}
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _timer!.Dispose();
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