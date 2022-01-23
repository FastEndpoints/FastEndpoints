namespace FastEndpoints;

internal static class MiscExtensions
{
    internal static Dictionary<TKey, List<TValue>> GroupToDictionary<TItem, TKey, TValue>(this IEnumerable<TItem> items,
        Func<TItem, TKey> keySelector,
        Func<TItem, TValue> valueSelector)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();

        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!dict.TryGetValue(key, out List<TValue>? grouping))
            {
                grouping = new List<TValue>(1);
                dict.Add(key, grouping);
            }
            grouping.Add(valueSelector(item));
        }

        return dict;
    }
}
