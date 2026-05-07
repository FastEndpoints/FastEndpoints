namespace FastEndpoints.OpenApi;

static class ParameterDescriptionLookup
{
    internal static Dictionary<string, string> Build(IReadOnlyDictionary<string, string> paramDescriptions)
    {
        var lookup = new Dictionary<string, string>(paramDescriptions.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, description) in paramDescriptions)
            lookup.TryAdd(key, description);

        return lookup;
    }
}
