using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Agents;

sealed class AgentEndpointCatalog<TEntry> where TEntry : class
{
    readonly Func<TEntry, string> _nameSelector;
    readonly Func<TEntry, EndpointDefinition> _definitionSelector;
    readonly string _pluralLabel;

    AgentEndpointCatalog(IReadOnlyList<TEntry> entries,
                         Func<TEntry, string> nameSelector,
                         Func<TEntry, EndpointDefinition> definitionSelector,
                         string pluralLabel)
    {
        Entries = entries;
        EntriesByName = entries.GroupBy(nameSelector, StringComparer.Ordinal)
                               .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        _nameSelector = nameSelector;
        _definitionSelector = definitionSelector;
        _pluralLabel = pluralLabel;
    }

    public IReadOnlyList<TEntry> Entries { get; }

    public IReadOnlyDictionary<string, TEntry[]> EntriesByName { get; }

    public static AgentEndpointCatalog<TEntry> FromEndpoints(IServiceProvider services,
                                                             Func<EndpointDefinition, TEntry?> createEntry,
                                                             Func<TEntry, string> nameSelector,
                                                             Func<TEntry, EndpointDefinition> definitionSelector,
                                                             string pluralLabel)
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var entries = new List<TEntry>();

        foreach (var def in endpointData.Found)
        {
            if (createEntry(def) is { } entry)
                entries.Add(entry);
        }

        return new(entries, nameSelector, definitionSelector, pluralLabel);
    }

    public IReadOnlyList<TEntry> GetVisible(ClaimsPrincipal principal,
                                            HttpContext context,
                                            Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> visibilityFilter)
        => Entries.Where(x => visibilityFilter(_definitionSelector(x), principal, context)).ToArray();

    public TEntry? ResolveVisible(string name,
                                  ClaimsPrincipal principal,
                                  HttpContext context,
                                  Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> visibilityFilter,
                                  string scope,
                                  string singularLabel)
    {
        if (!EntriesByName.TryGetValue(name, out var candidates))
            return null;

        var matches = candidates.Where(x => visibilityFilter(_definitionSelector(x), principal, context)).ToArray();

        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw AgentCatalogUniqueness.CreateDuplicateException(name, matches, scope, _definitionSelector, singularLabel, _pluralLabel)
        };
    }

    public void EnsureUnique(IReadOnlyCollection<TEntry> entries, string scope)
        => AgentCatalogUniqueness.EnsureUnique(entries, scope, _nameSelector, _definitionSelector, _pluralLabel);
}
