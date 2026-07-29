using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace PrefixedValueSource;

public class ParentPrefixIndexTests
{
    // Keys covering dotted, indexed, mixed, and exact-only shapes.
    static readonly string[] _sampleKeys =
    [
        "name",
        "nameExtra",
        "a.b.c",
        "items[0]",
        "items[0].name",
        "items[1].child.id",
        "Nested.Child",
        "Nested.Child.Value",
        "FOO.Bar",
        "a[0][1]",
        "a..b",
        "items[]"
    ];

    static readonly string[] _candidatePrefixes =
    [
        "name",
        "nameExtra",
        "a",
        "a.b",
        "a.b.c",
        "items",
        "items[0]",
        "items[1]",
        "items[1].child",
        "Nested",
        "Nested.Child",
        "foo",
        "FOO",
        "missing",
        "a[0]",
        "a.",
        "items["
    ];

    [Fact]
    public void QueryHasDataForPrefix_MatchesLegacyStartsWithRules()
    {
        var query = new QueryCollection(_sampleKeys.ToDictionary(k => k, _ => (StringValues)"1"));
        var source = new QueryValueSource(query);

        foreach (var prefix in _candidatePrefixes)
            source.HasDataForPrefix(prefix).ShouldBe(LegacyHasDataForPrefix(_sampleKeys, prefix), $"prefix: {prefix}");
    }

    [Fact]
    public void FormHasDataForPrefix_MatchesLegacyStartsWithRules_IncludingFiles()
    {
        var fieldKeys = new[] { "profile.name", "tags[0]" };
        var fileNames = new[] { "avatar.file", "docs[0].content" };
        var allKeys = fieldKeys.Concat(fileNames).ToArray();

        var files = new FormFileCollection();
        foreach (var name in fileNames)
            files.Add(new FormFile(Stream.Null, 0, 0, name, "x.bin"));

        var form = new FormCollection(
            fieldKeys.ToDictionary(k => k, _ => (StringValues)"1"),
            files);
        var source = new FormValueSource(form);

        var prefixes = new[]
        {
            "profile", "profile.name", "tags", "tags[0]",
            "avatar", "avatar.file", "docs", "docs[0]", "docs[0].content",
            "missing"
        };

        foreach (var prefix in prefixes)
            source.HasDataForPrefix(prefix).ShouldBe(LegacyHasDataForPrefix(allKeys, prefix), $"prefix: {prefix}");
    }

    [Fact]
    public void ExactKeyOnly_IsFalse()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["name"] = "x" });
        new QueryValueSource(query).HasDataForPrefix("name").ShouldBeFalse();
    }

    [Fact]
    public void NamePrefixWithoutBoundary_IsFalse()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["nameExtra"] = "x" });
        new QueryValueSource(query).HasDataForPrefix("name").ShouldBeFalse();
    }

    [Fact]
    public void CaseInsensitive_MatchesDifferentCasing()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["Nested.Child"] = "x" });
        var source = new QueryValueSource(query);

        source.HasDataForPrefix("nested").ShouldBeTrue();
        source.HasDataForPrefix("NESTED").ShouldBeTrue();
        source.HasDataForPrefix("nested.child").ShouldBeFalse(); // exact only after case fold; no further child
    }

    [Fact]
    public void EmptyCollections_IsFalse()
    {
        new QueryValueSource(QueryCollection.Empty).HasDataForPrefix("a").ShouldBeFalse();
        new FormValueSource(FormCollection.Empty).HasDataForPrefix("a").ShouldBeFalse();
    }

    [Fact]
    public void ParentPrefixIndex_SharedAcrossStructCopies()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["a.b"] = "1" });
        var first = new QueryValueSource(query);
        var second = first; // copy

        first.HasDataForPrefix("a").ShouldBeTrue();
        second.HasDataForPrefix("a").ShouldBeTrue();
    }

    [Fact]
    public void ParentPrefixIndex_Contains_MatchesLegacyForSampleKeys()
    {
        var index = ParentPrefixIndex.ForQuery(new QueryCollection(_sampleKeys.ToDictionary(k => k, _ => (StringValues)"1")));

        foreach (var prefix in _candidatePrefixes)
            index.Contains(prefix).ShouldBe(LegacyHasDataForPrefix(_sampleKeys, prefix), $"prefix: {prefix}");
    }

    /// <summary>
    /// Previous FormValueSource / QueryValueSource algorithm (full key scan).
    /// </summary>
    static bool LegacyHasDataForPrefix(IEnumerable<string> keys, string prefix)
    {
        var dottedPrefix = $"{prefix}.";
        var indexedPrefix = $"{prefix}[";

        return keys.Any(
            k => k.StartsWith(dottedPrefix, StringComparison.OrdinalIgnoreCase) ||
                 k.StartsWith(indexedPrefix, StringComparison.OrdinalIgnoreCase));
    }
}