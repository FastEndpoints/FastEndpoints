namespace FastEndpoints.Testing;

static class PriorityOrdering
{
    internal static IReadOnlyCollection<T> OrderByPriority<T>(IReadOnlyCollection<T> items, Func<T, int?> getPriority)
    {
        var ordered = new List<(int priority, T item)>();
        var unordered = new List<T>();

        foreach (var item in items)
        {
            var priority = getPriority(item);

            if (priority is not null)
                ordered.Add((priority.Value, item));
            else
                unordered.Add(item);
        }

        return ordered.OrderBy(t => t.priority)
                      .Select(t => t.item)
                      .Union(unordered)
                      .ToArray();
    }
}
