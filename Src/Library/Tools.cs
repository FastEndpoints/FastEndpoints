using static FastEndpoints.Config;

namespace FastEndpoints;

internal static class MiscExtensions
{
    internal static Dictionary<TKey, List<TValue>> GroupToDictionary<TItem, TKey, TValue>(this List<TItem> items,
        Func<TItem, TKey> keySelector, Func<TItem, TValue> valueSelector)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();

        for (int i = 0; i < items.Count; i++)
        {
            TItem item = items[i];
            var key = keySelector(item);
            if (!dict.TryGetValue(key, out List<TValue>? group))
            {
                group = new List<TValue>(1);
                dict.Add(key, group);
            }
            group.Add(valueSelector(item));
        }

        return dict;
    }

    internal static string EndpointName(this Type epType, string? verb = null, int? routeNum = null)
    {
        var vrb = verb != null ? verb[0] + verb[1..].ToLowerInvariant() : null;
        var ep = ShortEpNames ? epType.Name : epType.FullName!.Replace(".", string.Empty);
        return vrb + ep + routeNum.ToString();
    }
}
