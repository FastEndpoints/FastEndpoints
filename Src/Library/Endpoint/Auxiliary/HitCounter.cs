using System.Collections.Concurrent;

namespace FastEndpoints;

sealed class HitCounter
{
    //key: header value as a unique client identifier
    ConcurrentDictionary<string, Counter> _clients = new();
    readonly double _durationSeconds;
    readonly int _limit;

    internal string? HeaderName { get; }

    internal HitCounter(string? headerName, double durationSeconds, int hitLimit)
    {
        HeaderName = headerName;
        _durationSeconds = durationSeconds;
        _limit = hitLimit;
    }

    internal bool LimitReached(string headerValue)
    {
        return _clients.GetOrAdd(headerValue, CounterInitializer).LimitReached();

        Counter CounterInitializer(string hVal)
            => new(_durationSeconds, hVal, _limit, ref _clients);
    }

    sealed class Counter : IDisposable
    {
        readonly string _self;
        readonly ConcurrentDictionary<string, Counter> _parent;
        Timer? _timer;
        long _expireAtTicks;
        readonly double _durationSecs;
        readonly int _limit;
        int _count;

        bool Expired => DateTime.UtcNow.Ticks > _expireAtTicks;

        internal Counter(double durationSeconds, string key, int limit, ref ConcurrentDictionary<string, Counter> dictionary)
        {
            _self = key;
            _limit = limit;
            _parent = dictionary;
            _durationSecs = durationSeconds;
            _timer = new(RemoveFromParentIfExpired, null, 60000, 60000); //cleanup every 60 secs.
            _expireAtTicks = GetNewExpiry();
        }

        long GetNewExpiry()
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

        void RemoveFromParentIfExpired(object? _)
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

        bool _disposedValue;

        void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _timer!.Dispose();
                    _timer = null;
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~Counter()
        {
            Dispose(false);
        }
    }
}