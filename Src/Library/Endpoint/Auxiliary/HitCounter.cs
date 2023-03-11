using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class HitCounter
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
        private long _expireAtTicks;
        private readonly double _durationSecs;
        private readonly int _limit;
        private int _count;

        private bool Expired => DateTime.UtcNow.Ticks > _expireAtTicks;

        internal Counter(double durationSeconds, string key, int limit, ref ConcurrentDictionary<string, Counter> dictionary)
        {
            _self = key;
            _limit = limit;
            _parent = dictionary;
            _durationSecs = durationSeconds;
            _timer = new(RemoveFromParentIfExpired, null, 60000, 60000);//cleanup every 60 secs.
            _expireAtTicks = GetNewExpiry();
        }

        private long GetNewExpiry()
            => DateTime.UtcNow.AddSeconds(_durationSecs).Ticks;

        internal bool LimitReached()
        {
            if (Expired)
            {
                Interlocked.Exchange(ref _expireAtTicks, GetNewExpiry());
                Interlocked.Exchange(ref _count, 1);
                //Console.WriteLine($"reset: {GetHashCode()}");
                return false;
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

            if (Expired && _parent.TryRemove(_self, out var _))
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