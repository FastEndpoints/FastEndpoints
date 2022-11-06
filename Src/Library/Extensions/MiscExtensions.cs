namespace FastEndpoints;

internal static class MiscExtensions
{
    internal static Dictionary<TKey, List<TValue>> GroupToDictionary<TItem, TKey, TValue>
        (this List<TItem> items,
         Func<TItem, TKey> keySelector,
         Func<TItem, TValue> valueSelector) where TKey : notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var key = keySelector(item);
            if (!dict.TryGetValue(key, out var group))
            {
                group = new(1);
                dict.Add(key, group);
            }
            group.Add(valueSelector(item));
        }

        return dict;
    }
}