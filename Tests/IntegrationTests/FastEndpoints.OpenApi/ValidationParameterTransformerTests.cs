namespace OpenApi;

public class ValidationParameterTransformerTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task fluent_validation_rules_apply_to_get_query_and_path_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/query-param-validation/{id}"]!["get"]!["parameters"].ArrayItems().ToArray();

        var id = Param(parameters, "id", "path");
        id["required"]!.GetValue<bool>().ShouldBeTrue();
        id["schema"]!["minLength"]!.GetValue<int>().ShouldBe(2);

        var term = Param(parameters, "q", "query");
        term["required"]!.GetValue<bool>().ShouldBeTrue();
        term["schema"]!["minLength"]!.GetValue<int>().ShouldBe(3);

        var page = Param(parameters, "page", "query");
        page["required"]!.GetValue<bool>().ShouldBeTrue();
        page["schema"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(0);

        var optionalFilter = Param(parameters, "optionalFilter", "query");
        optionalFilter["required"].ShouldBeNull();
        optionalFilter["schema"]!["maxLength"]!.GetValue<int>().ShouldBe(20);

        var conditionalName = Param(parameters, "conditionalName", "query");
        conditionalName["required"].ShouldBeNull();
        conditionalName["schema"]!["minLength"].ShouldBeNull();
    }

    [Fact]
    public async Task fluent_validation_rules_apply_to_query_header_and_body_on_mixed_post()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var operation = doc["paths"]!["/api/swagger-review/query-param-validation-mixed"]!["post"]!;
        var parameters = operation["parameters"].ArrayItems().ToArray();

        var search = Param(parameters, "search", "query");
        search["required"]!.GetValue<bool>().ShouldBeTrue();
        search["schema"]!["minLength"]!.GetValue<int>().ShouldBe(2);

        var client = Param(parameters, "X-Client", "header");
        (client["required"]?.GetValue<bool>() ?? false).ShouldBeFalse();
        client["schema"]!["minLength"]!.GetValue<int>().ShouldBe(2);

        var requestSchema = ResolveSchema(doc, operation["requestBody"]!["content"]!["application/json"]!["schema"]!);
        requestSchema["properties"]!["bodyValue"]!["minLength"]!.GetValue<int>().ShouldBe(5);
        requestSchema["properties"]!["search"].ShouldBeNull();
        requestSchema["properties"]!["client"].ShouldBeNull();
    }

    [Fact]
    public async Task fluent_validation_rules_match_json_property_name_to_query_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/query-param-validation-names"]!["get"]!["parameters"].ArrayItems().ToArray();

        var searchTerm = Param(parameters, "searchTerm", "query");
        searchTerm["required"]!.GetValue<bool>().ShouldBeTrue();
        searchTerm["schema"]!["minLength"]!.GetValue<int>().ShouldBe(4);
    }

    static JsonNode Param(JsonNode[] parameters, string name, string location)
        => parameters.Single(p => p["name"]!.GetValue<string>() == name && p["in"]!.GetValue<string>() == location);

    static JsonNode ResolveSchema(JsonNode document, JsonNode schema)
    {
        var refValue = schema["$ref"]?.GetValue<string>();

        if (refValue is null)
            return schema;

        var schemaKey = refValue[(refValue.LastIndexOf('/') + 1)..];

        return document["components"]!["schemas"]![schemaKey]!;
    }
}

static file class JsonNodeTestExtensions
{
    public static IEnumerable<JsonNode> ArrayItems(this JsonNode? node)
        => node is JsonArray arr
               ? arr.OfType<JsonNode>()
               : [];
}
