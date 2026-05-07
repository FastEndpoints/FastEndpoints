namespace FastEndpoints.OpenApi;

static class ParameterDescriptionLookup
{
    internal static Dictionary<string, string> Build(IReadOnlyDictionary<string, string> paramDescriptions)
        => paramDescriptions.ToCaseInsensitiveDictionary(paramDescriptions.Count);
}
