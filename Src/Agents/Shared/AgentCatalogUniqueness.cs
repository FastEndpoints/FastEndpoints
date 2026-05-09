namespace FastEndpoints.Agents;

static class AgentCatalogUniqueness
{
    internal static void EnsureUnique<T>(IReadOnlyCollection<T> items,
                                         string scope,
                                         Func<T, string> keySelector,
                                         Func<T, EndpointDefinition> definitionSelector,
                                         string pluralLabel)
    {
        var collisions = items.GroupBy(keySelector, StringComparer.Ordinal)
                              .Where(g => g.Count() > 1)
                              .ToArray();

        if (collisions.Length == 0)
            return;

        throw new InvalidOperationException(
            $"Duplicate {pluralLabel} detected among " +
            scope +
            ": " +
            string.Join(
                "; ",
                collisions.Select(
                    g => $"'{g.Key}' => {FormatEndpointTypeNames(g, definitionSelector)}")) +
            $". {pluralLabel} must be unique.");
    }

    internal static InvalidOperationException CreateDuplicateException<T>(string key,
                                                                         IReadOnlyCollection<T> matches,
                                                                         string scope,
                                                                         Func<T, EndpointDefinition> definitionSelector,
                                                                         string singularLabel,
                                                                         string pluralLabel)
        => new(
            $"Duplicate {singularLabel} '{key}' detected among {scope}: " +
            FormatEndpointTypeNames(matches, definitionSelector) +
            $". {pluralLabel} must be unique.");

    static string FormatEndpointTypeNames<T>(IEnumerable<T> items, Func<T, EndpointDefinition> definitionSelector)
        => string.Join(", ", items.Select(x => definitionSelector(x).EndpointType.FullName ?? definitionSelector(x).EndpointType.Name).Distinct(StringComparer.Ordinal));
}
