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
/// the previous <c>key.</c> / <c>key[</c> StartsWith rules.
/// <para>
/// Prefixes are stored as (<see cref="string"/> source, length) spans into the original keys, with no per-segment
/// substring allocations.
/// </para>
/// </summary>
sealed class ParentPrefixIndex
{
    const int MinCapacity = 8;

    readonly IEnumerable<string> _keys;
    readonly IFormFileCollection? _files;

    // Open-addressed table: each entry is a slice of an original key (source + length).
    string?[]? _sources;
    int[]? _lengths;
    int[]? _hashes;
    int _mask;
    int _count;

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
        EnsureInitialized();

        if (_count == 0)
            return false;

        var hash = string.GetHashCode(key.AsSpan(), StringComparison.OrdinalIgnoreCase);
        var sources = _sources!;
        var lengths = _lengths!;
        var hashes = _hashes!;
        var mask = _mask;
        var i = hash & mask;

        while (true)
        {
            var src = sources[i];

            if (src is null)
                return false;

            if (hashes[i] == hash &&
                lengths[i] == key.Length &&
                key.AsSpan().Equals(src.AsSpan(0, lengths[i]), StringComparison.OrdinalIgnoreCase))
                return true;

            i = (i + 1) & mask;
        }
    }

    void EnsureInitialized()
    {
        if (_sources is not null)
            return;

        // Single pass: optional size hint from key cardinality; unique prefixes drive growth.
        var capacity = MinCapacity;

        if (_keys is ICollection<string> kc)
        {
            var keyCount = kc.Count;

            if (_files is not null)
                keyCount += _files.Count;

            if (keyCount == 0)
            {
                _sources = [];
                _lengths = [];
                _hashes = [];
                _mask = 0;

                return;
            }

            // ~2 parent segments/key is common; load factor 0.5 → capacity ≈ 4× keys.
            var estimate = keyCount * 4;

            while (capacity < estimate)
                capacity <<= 1;
        }

        _sources = new string?[capacity];
        _lengths = new int[capacity];
        _hashes = new int[capacity];
        _mask = capacity - 1;

        foreach (var k in _keys)
            AddParentPrefixes(k);

        if (_files is not null)
        {
            foreach (var file in _files)
            {
                if (file.Name is { Length: > 0 } name)
                    AddParentPrefixes(name);
            }
        }

        // No nested prefixes (flat keys only): drop the empty table.
        if (_count == 0)
        {
            _sources = [];
            _lengths = [];
            _hashes = [];
            _mask = 0;
        }
    }

    /// <summary>
    /// For each <c>.</c> or <c>[</c> in <paramref name="key"/>, register the span before it.
    /// Mirrors StartsWith(<c>prefix.</c>) / StartsWith(<c>prefix[</c>) used previously.
    /// </summary>
    void AddParentPrefixes(string key)
    {
        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] is '.' or '[')
                TryAdd(key, i);
        }
    }

    void TryAdd(string source, int length)
    {
        // Keep load factor ≤ 0.5 for linear probing.
        if (_count * 2 >= _sources!.Length)
            Resize();

        var hash = string.GetHashCode(source.AsSpan(0, length), StringComparison.OrdinalIgnoreCase);
        Insert(source, length, hash);
    }

    void Insert(string source, int length, int hash)
    {
        var sources = _sources!;
        var lengths = _lengths!;
        var hashes = _hashes!;
        var mask = _mask;
        var i = hash & mask;

        while (true)
        {
            var src = sources[i];

            if (src is null)
            {
                sources[i] = source;
                lengths[i] = length;
                hashes[i] = hash;
                _count++;

                return;
            }

            if (hashes[i] == hash &&
                lengths[i] == length &&
                source.AsSpan(0, length).Equals(src.AsSpan(0, length), StringComparison.OrdinalIgnoreCase))
                return; // duplicate prefix from another key

            i = (i + 1) & mask;
        }
    }

    void Resize()
    {
        var oldSources = _sources!;
        var oldLengths = _lengths!;
        var oldHashes = _hashes!;
        var oldCap = oldSources.Length;
        var newCap = oldCap < MinCapacity ? MinCapacity : oldCap << 1;

        _sources = new string?[newCap];
        _lengths = new int[newCap];
        _hashes = new int[newCap];
        _mask = newCap - 1;
        _count = 0;

        for (var i = 0; i < oldCap; i++)
        {
            if (oldSources[i] is { } src)
                Insert(src, oldLengths[i], oldHashes[i]);
        }
    }
}
