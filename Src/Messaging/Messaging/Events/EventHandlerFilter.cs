using System.Buffers;

namespace FastEndpoints;

interface IEventHandlerType<THandler>
{
    static abstract Type GetHandlerType(THandler handler);
}

static class EventHandlerFilter<THandler, THandlerType> where THandlerType : struct, IEventHandlerType<THandler>
{
    internal static int[] CreateTypeIndex(THandler[] handlers)
    {
        if (handlers.Length < 2)
            return [];

        Dictionary<Type, int> firstByType = [];
        var indexes = new int[handlers.Length];
        for (var i = 0; i < handlers.Length; i++)
        {
            var type = THandlerType.GetHandlerType(handlers[i]);
            if (!firstByType.TryGetValue(type, out var first))
                firstByType.Add(type, first = i);
            indexes[i] = first;
        }
        return indexes;
    }

    internal static THandler[] Filter(THandler[] handlers, int[] firstHandlerOfType, Func<Type, bool> filter)
    {
        var len = handlers.Length;
        if (len == 0)
            return handlers;
        if (len == 1)
            return filter(THandlerType.GetHandlerType(handlers[0])) ? handlers : [];

        // Only type indexes are shared. Each publish owns its decisions, including reentrant filters.
        bool[]? rented = null;
        Span<bool> decisions = len <= 128 ? stackalloc bool[len] : (rented = ArrayPool<bool>.Shared.Rent(len));
        try
        {
            var matchCount = 0;
            for (var i = 0; i < len; i++)
            {
                var first = firstHandlerOfType[i];
                if (first == i)
                    decisions[first] = filter(THandlerType.GetHandlerType(handlers[i]));
                if (decisions[first])
                    matchCount++;
            }

            if (matchCount == 0)
                return [];
            if (matchCount == len)
                return handlers;

            var filtered = new THandler[matchCount];
            var j = 0;
            for (var i = 0; i < len; i++)
            {
                if (decisions[firstHandlerOfType[i]])
                    filtered[j++] = handlers[i];
            }
            return filtered;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<bool>.Shared.Return(rented);
        }
    }
}
