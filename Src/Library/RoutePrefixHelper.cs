namespace FastEndpoints;

internal static class RoutePrefixHelper
{
    internal static string? Normalize(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return null;

        var trimmed = prefix.AsSpan().Trim('/');

        return trimmed.IsEmpty ? null : trimmed.ToString();
    }

    internal static string? Resolve(string? globalPrefix, string? prefixOverride)
    {
        var normalizedGlobal = Normalize(globalPrefix);

        if (normalizedGlobal is null)
            return null;

        if (prefixOverride == string.Empty)
            return null;

        return Normalize(prefixOverride) ?? normalizedGlobal;
    }
}
