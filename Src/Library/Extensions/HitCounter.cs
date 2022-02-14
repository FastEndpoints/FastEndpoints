using System.Collections.Concurrent;

namespace FastEndpoints;

internal class HitCounter
{
    //key: header value (specified by user in endpoint config)
    private readonly ConcurrentDictionary<string, Counter> dic = new();
    private readonly double _duration;
    private readonly int _limit;

    internal HitCounter(double durationSeconds, int hitLimit)
    {
        _duration = durationSeconds;
        _limit = hitLimit;
    }

    internal void RecordHit(string headerValue)
        => dic.GetOrAdd(headerValue, new Counter(_limit, _duration, headerValue, dic));

    internal bool LimitReached(string headerValue)
        => dic.GetOrAdd(headerValue, new Counter(_limit, _duration, headerValue, dic)).LimitReached;

    private class Counter
    {
        private int _count = 1;
        private readonly string _key;
        private readonly ConcurrentDictionary<string, Counter> _dictionary;
        private readonly int _limit;
        private readonly System.Timers.Timer _timer;

        internal bool LimitReached => _count >= _limit;

        internal Counter(int limit, double durationSeconds, string key, ConcurrentDictionary<string, Counter> dictionary)
        {
            _limit = limit;
            _key = key;
            _dictionary = dictionary;
            _timer = new(durationSeconds) { AutoReset = false };
            _timer.Elapsed += RemoveCounter;
            _timer.Start();
        }

        private void RemoveCounter(object? _, System.Timers.ElapsedEventArgs __)
        {
            _dictionary.TryRemove(_key, out var _);
            _timer?.Dispose();
        }

        internal void Increase() => Interlocked.Increment(ref _count);
    }
}