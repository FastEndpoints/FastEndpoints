namespace OpenApi;

public class NestedGroupTagTests(Fixture App) : TestBase<Fixture>
{
    const string Path = "/api/first/middle/last/item";

    [Fact]
    public async Task nested_group_tags_are_preserved_when_auto_tagging_is_disabled()
    {
        var doc = JsonNode.Parse(await App.GetDocumentJsonAsync("Nested Group Tags"))!;
        var tags = TagNames(doc["paths"]![Path]!["get"]!["tags"]);

        ShouldHaveTags(tags, "First", "Middle", "Last", "Endpoint");

        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tag in (JsonArray)doc["tags"]!)
        {
            var name = tag?["name"]?.GetValue<string>();
            var description = tag?["description"]?.GetValue<string>();

            if (name is not null && description is not null)
                descriptions[name] = description;
        }

        descriptions["First"].ShouldBe("Root group");
        descriptions["Middle"].ShouldBe("Middle group");
        descriptions["Last"].ShouldBe("Immediate group");
        descriptions["Endpoint"].ShouldBe("Endpoint tag");
    }

    [Fact]
    public async Task shared_document_keeps_explicit_tags_when_auto_tagging_is_enabled()
    {
        var tags = TagNames(JsonNode.Parse(await App.GetDocumentJsonAsync("Release 1.0"))!["paths"]![Path]!["get"]!["tags"]);

        ShouldHaveTags(tags, "First", "Middle", "Last", "Endpoint");
    }

    static string[] TagNames(JsonNode? node)
        => node is JsonArray array
               ? array.Select(static item => item!.GetValue<string>()).ToArray()
               : [];

    static void ShouldHaveTags(string[] actual, params string[] expected)
    {
        actual.Distinct(StringComparer.Ordinal).Count().ShouldBe(actual.Length);
        actual.OrderBy(static name => name, StringComparer.Ordinal)
              .ShouldBe(expected.OrderBy(static name => name, StringComparer.Ordinal).ToArray());
    }
}
