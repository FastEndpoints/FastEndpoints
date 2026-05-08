using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenApi;

static class JsonSnapshotComparer
{
    public static void AssertMatches(string currentJson, string snapshot)
    {
        var expectedDoc = JsonNode.Parse(snapshot);
        var currentDoc = JsonNode.Parse(currentJson);
        var diffs = new List<string>();

        CollectJsonDiffs(expectedDoc, currentDoc, "$", diffs);

        if (diffs.Count > 0)
            Assert.Fail("Snapshot differs:\n" + string.Join('\n', diffs));
    }

    static void CollectJsonDiffs(JsonNode? expected, JsonNode? actual, string path, List<string> diffs)
    {
        if (DiffLimitReached(diffs))
            return;

        if (expected is null || actual is null)
        {
            if (expected is not null || actual is not null)
                AddJsonDiff(diffs, $"~ {path}: expected {FormatValue(expected)}, actual {FormatValue(actual)}");

            return;
        }

        var expectedType = JsonNodeType(expected);
        var actualType = JsonNodeType(actual);

        if (expectedType != actualType)
        {
            AddJsonDiff(diffs, $"~ {path}: expected {expectedType}, actual {actualType}");

            return;
        }

        switch (expected)
        {
            case JsonObject expectedObj when actual is JsonObject actualObj:
                CompareObjects(expectedObj, actualObj, path, diffs);
                break;

            case JsonArray expectedArr when actual is JsonArray actualArr:
                CompareArrays(expectedArr, actualArr, path, diffs);
                break;

            default:
                if (!JsonNode.DeepEquals(expected, actual))
                    AddJsonDiff(diffs, $"~ {path}: expected {FormatValue(expected)}, actual {FormatValue(actual)}");
                break;
        }
    }

    static void CompareObjects(JsonObject expected, JsonObject actual, string path, List<string> diffs)
    {
        var expectedProps = expected.ToDictionary(p => p.Key, StringComparer.Ordinal);
        var actualProps = actual.ToDictionary(p => p.Key, StringComparer.Ordinal);

        foreach (var (name, expectedProp) in expectedProps)
        {
            var propPath = JsonPath(path, name);

            if (!actualProps.TryGetValue(name, out var actualProp))
            {
                AddJsonDiff(diffs, $"- {propPath}: missing from current document");

                continue;
            }

            CollectJsonDiffs(expectedProp.Value, actualProp.Value, propPath, diffs);
        }

        foreach (var name in actualProps.Keys.Except(expectedProps.Keys, StringComparer.Ordinal))
            AddJsonDiff(diffs, $"+ {JsonPath(path, name)}: unexpected in current document");
    }

    static void CompareArrays(JsonArray expected, JsonArray actual, string path, List<string> diffs)
    {
        if (expected.Count != actual.Count)
            AddJsonDiff(diffs, $"~ {path}: expected {expected.Count} items, actual {actual.Count} items");

        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
            CollectJsonDiffs(expected[i], actual[i], $"{path}[{i}]", diffs);
    }

    static string JsonPath(string path, string propertyName)
        => propertyName.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_')
               ? $"{path}.{propertyName}"
               : $"{path}['{propertyName.Replace("'", "\\'")}']";

    static string FormatValue(JsonNode? node)
        => node?.ToJsonString(new() { WriteIndented = false }) ?? "null";

    static string JsonNodeType(JsonNode node)
        => node switch
        {
            JsonObject => nameof(JsonObject),
            JsonArray => nameof(JsonArray),
            JsonValue value => value.GetValue<JsonElement>().ValueKind.ToString(),
            _ => node.GetType().Name
        };

    static void AddJsonDiff(List<string> diffs, string diff)
    {
        const int maxDiffs = 25;

        if (DiffLimitReached(diffs))
            return;

        diffs.Add(diff);

        if (diffs.Count == maxDiffs)
            diffs.Add($"... stopped after {maxDiffs} differences");
    }

    static bool DiffLimitReached(List<string> diffs)
        => diffs.Count > 0 && diffs[^1].StartsWith("... stopped after", StringComparison.Ordinal);
}
