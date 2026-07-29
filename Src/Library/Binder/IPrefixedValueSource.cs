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
    // Class-backed so struct copies (recursive bind) share one index.
    readonly ParentPrefixIndex _prefixes = ParentPrefixIndex.ForForm(form);

    public string SourceName => "form";
    public bool SupportsFiles => true;

    public bool TryGetValues(string key, out StringValues values)
        => form.TryGetValue(key, out values);

    public bool HasDataForPrefix(string key)
        => _prefixes.Contains(key);

    public IFormFile? GetFile(string key)
        => form.Files.GetFile(key);

    public IReadOnlyList<IFormFile> GetFiles(string key)
        => form.Files.GetFiles(key);
}

readonly struct QueryValueSource(IQueryCollection query) : IPrefixedValueSource
{
    // Class-backed so struct copies (recursive bind) share one index.
    readonly ParentPrefixIndex _prefixes = ParentPrefixIndex.ForQuery(query);

    public string SourceName => "query param";
    public bool SupportsFiles => false;

    public bool TryGetValues(string key, out StringValues values)
        => query.TryGetValue(key, out values);

    public bool HasDataForPrefix(string key)
        => _prefixes.Contains(key);

    public IFormFile? GetFile(string key)
        => null;

    public IReadOnlyList<IFormFile> GetFiles(string key)
        => [];
}

/// <summary>
/// One-shot index of parent prefixes present in form/query keys so
/// <see cref="IPrefixedValueSource.HasDataForPrefix"/> is O(1) instead of scanning all keys per nested property.
/// A key <c>a.b[0].c</c> yields prefixes <c>a</c>, <c>a.b</c>, <c>a.b[0]</c> (case-insensitive), matching
/// the previous <c>key.</c> / <c>key[</c> StartsWith rules without per-call string allocations.
/// </summary>
sealed class ParentPrefixIndex
{
    readonly IEnumerable<string> _keys;
    readonly IFormFileCollection? _files;
    HashSet<string>? _prefixes;

    ParentPrefixIndex(IEnumerable<string> keys, IFormFileCollection? files)
    {
        _keys = keys;
        _files = files;
    }

    public static ParentPrefixIndex ForForm(IFormCollection form)
        => new(form.Keys, form.Files);

    public static ParentPrefixIndex ForQuery(IQueryCollection query)
        => new(query.Keys, files: null);

    public bool Contains(string key)
    {
        if (_prefixes is null)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var k in _keys)
                AddParentPrefixes(set, k);

            if (_files is not null)
            {
                foreach (var file in _files)
                {
                    if (file.Name is { Length: > 0 } name)
                        AddParentPrefixes(set, name);
                }
            }

            _prefixes = set;
        }

        return _prefixes.Contains(key);
    }

    /// <summary>
    /// For each <c>.</c> or <c>[</c> in <paramref name="key"/>, add the substring before it.
    /// Mirrors StartsWith(<c>prefix.</c>) / StartsWith(<c>prefix[</c>) used previously.
    /// </summary>
    static void AddParentPrefixes(HashSet<string> set, string key)
    {
        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] is '.' or '[')
                set.Add(key[..i]);
        }
    }
}
