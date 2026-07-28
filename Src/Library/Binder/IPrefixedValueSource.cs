using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

interface IPrefixedValueSource
{
    /// <summary>
    /// Short name used in binding error messages (e.g. "form", "query param").
    /// </summary>
    string SourceName { get; }

    bool SupportsFiles { get; }

    bool TryGetValues(string key, out StringValues values);

    bool HasDataForPrefix(string key);

    IFormFile? GetFile(string key);

    IReadOnlyList<IFormFile> GetFiles(string key);
}

readonly struct FormValueSource(IFormCollection form) : IPrefixedValueSource
{
    public string SourceName => "form";
    public bool SupportsFiles => true;

    public bool TryGetValues(string key, out StringValues values)
        => form.TryGetValue(key, out values);

    public bool HasDataForPrefix(string key)
    {
        var dottedPrefix = $"{key}.";
        var indexedPrefix = $"{key}[";

        return form.Keys.Any(MatchesPrefix) || form.Files.Any(f => MatchesPrefix(f.Name));

        bool MatchesPrefix(string candidate)
            => candidate.StartsWith(dottedPrefix, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(indexedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public IFormFile? GetFile(string key)
        => form.Files.GetFile(key);

    public IReadOnlyList<IFormFile> GetFiles(string key)
        => form.Files.GetFiles(key);
}

readonly struct QueryValueSource(IQueryCollection query) : IPrefixedValueSource
{
    public string SourceName => "query param";
    public bool SupportsFiles => false;

    public bool TryGetValues(string key, out StringValues values)
        => query.TryGetValue(key, out values);

    public bool HasDataForPrefix(string key)
    {
        var dottedPrefix = $"{key}.";
        var indexedPrefix = $"{key}[";

        return query.Keys.Any(
            k => k.StartsWith(dottedPrefix, StringComparison.OrdinalIgnoreCase) ||
                 k.StartsWith(indexedPrefix, StringComparison.OrdinalIgnoreCase));
    }

    public IFormFile? GetFile(string key)
        => null;

    public IReadOnlyList<IFormFile> GetFiles(string key)
        => [];
}
