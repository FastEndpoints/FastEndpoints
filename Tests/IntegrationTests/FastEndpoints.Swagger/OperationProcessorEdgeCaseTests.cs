namespace Swagger;

public class OperationProcessorEdgeCaseTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task auto_tag_override_uses_override_value()
    {
        var doc = await App.DocGenerator.GenerateAsync("Swagger Review");
        var tags = JToken.Parse(doc.ToJson())["paths"]!["/api/swagger-review/auto-tag-override"]!["get"]!["tags"]!
                         .Values<string>()
                         .ToArray();

        tags.ShouldBe(["ReviewTag"]);
    }

    [Fact]
    public async Task duplicate_request_example_labels_are_indexed()
    {
        var doc = await App.DocGenerator.GenerateAsync("Swagger Review");
        var examples =
            (JObject)JToken.Parse(doc.ToJson())["paths"]!["/api/swagger-review/duplicate-examples"]!["post"]!["requestBody"]!["content"]!["application/json"]!["examples"]!;

        examples.Properties().Select(p => p.Name).ToArray().ShouldBe(["Example 1", "Example 2"]);
    }

    [Fact]
    public async Task illegal_header_names_are_not_added_as_parameters()
    {
        var doc = await App.DocGenerator.GenerateAsync("Swagger Review");
        var parameters = JToken.Parse(doc.ToJson())["paths"]!["/api/swagger-review/illegal-headers"]!["post"]!["parameters"];

        parameters.ShouldBeNull();
    }

    [Fact]
    public async Task empty_request_schemas_are_removed_when_option_is_enabled()
    {
        var doc = await App.DocGenerator.GenerateAsync("Swagger Review Empty Schema");
        var json = JToken.Parse(doc.ToJson());
        var operation = json["paths"]!["/api/swagger-review/empty-schema-cleanup"]!["post"]!;

        operation["requestBody"].ShouldBeNull();
        json["components"]!["schemas"]!["TestCasesSwaggerReviewEmptySchemaCleanupRequest"].ShouldBeNull();
    }
}