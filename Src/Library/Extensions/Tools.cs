namespace FastEndpoints;

internal static class MiscExtensions
{
    internal static Dictionary<TKey, List<TValue>> GroupToDictionary<TItem, TKey, TValue>(this List<TItem> items,
        Func<TItem, TKey> keySelector,
        Func<TItem, TValue> valueSelector)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();

        for (int i = 0; i < items.Count; i++)
        {
            TItem item = items[i];
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
