namespace OpenApi;

public class OperationTransformerEdgeCaseTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task swagger_generation_is_stable_across_multiple_runs()
    {
        var first = await App.GetDocumentJsonAsync("Swagger Review");
        var second = await App.GetDocumentJsonAsync("Swagger Review");

        JToken.Parse(first).ShouldBeEquivalentTo(JToken.Parse(second));
    }

    [Fact]
    public async Task auto_tag_override_uses_override_value()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var tags = JToken.Parse(json)["paths"]!["/api/swagger-review/auto-tag-override"]!["get"]!["tags"]!
                         .Values<string>()
                         .ToArray();

        tags.ShouldBe(["ReviewTag"]);
    }

    [Fact]
    public async Task duplicate_request_example_labels_are_indexed()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var examples = (JObject)JToken.Parse(json)["paths"]!["/api/swagger-review/duplicate-examples"]!["post"]!
                                                 ["requestBody"]!["content"]!["application/json"]!["examples"]!;

        examples.Properties().Select(p => p.Name).ToArray().ShouldBe(["Example 1", "Example 2"]);
    }

    [Fact]
    public async Task illegal_header_names_are_not_added_as_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JToken.Parse(json)["paths"]!["/api/swagger-review/illegal-headers"]!["post"]!["parameters"];

        parameters.ShouldBeNull();
    }

    [Fact]
    public async Task empty_request_schemas_are_removed_when_option_is_enabled()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review Empty Schema");
        var doc = JToken.Parse(json);
        var operation = doc["paths"]!["/api/swagger-review/empty-schema-cleanup"]!["post"]!;

        operation["requestBody"].ShouldBeNull();
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewEmptySchemaCleanupRequest"].ShouldBeNull();
    }

    [Fact]
    public async Task from_body_property_replaces_request_body_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JToken.Parse(json)["paths"]!["/api/test-cases/from-body-binding/{id}"]!["post"]!;

        operation["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>()
                                                                                      .ShouldBe("#/components/schemas/TestCasesFromBodyJsonBindingProduct");
        operation["parameters"]!.SelectToken("$[?(@.name=='customerID')].in")!.Value<string>().ShouldBe("header");
        operation["parameters"]!.SelectToken("$[?(@.name=='id')].in")!.Value<string>().ShouldBe("path");
    }

    [Fact]
    public async Task json_patch_request_body_uses_json_patch_document_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var content = JToken.Parse(json)["paths"]!["/api/json-patch-test/{id}"]!["patch"]!["requestBody"]!["content"]!;

        // MS OpenApi generates a proper JsonPatchDocument<T> schema using the framework's built-in type
        // instead of NSwag's incorrect type:object. see accepted differences in the port review.
        var schema = content["application/json-patch+json"]!["schema"]!;
        var refId = schema["$ref"]!.Value<string>()!;
        refId.ShouldContain("JsonPatchDocument");
        refId.ShouldContain("Person");
    }

    [Fact]
    public async Task typed_result_endpoint_keeps_response_shapes()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var responses = JToken.Parse(json)["paths"]!["/api/multi-test"]!["post"]!["responses"]!;

        responses["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>()
                                                                              .ShouldBe("#/components/schemas/TestCasesTypedResultTestResponse");
        responses["400"]!["content"]!["application/problem+json"]!["schema"]!["$ref"]!.Value<string>()
                                                                                      .ShouldBe("#/components/schemas/FastEndpointsProblemDetails");
        responses["404"]!["description"]!.Value<string>().ShouldBe("Not Found");
    }

    [Fact]
    public async Task idempotency_header_is_added_as_required_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var header = JToken.Parse(json)["paths"]!["/api/test-cases/idempotency/{id}"]!["get"]!["parameters"]!
                           .First(p => p["name"]!.Value<string>() == "Idempotency-Key");

        header["in"]!.Value<string>().ShouldBe("header");
        header["required"]!.Value<bool>().ShouldBeTrue();
        header["schema"]!["type"]!.Value<string>().ShouldBe("string");
    }

    [Fact]
    public async Task idempotency_header_without_explicit_type_uses_example_shape()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var header = JToken.Parse(json)["paths"]!["/api/swagger-review/idempotency-anonymous-example"]!["post"]!["parameters"]!
                           .First(p => p["name"]!.Value<string>() == "Idempotency-Key");

        header["description"]!.Value<string>().ShouldBe("custom idempotency header");
        header["schema"]!["$ref"].ShouldBeNull();
        header["schema"]!["type"]!.Value<string>().ShouldBe("object");
        header["schema"]!["properties"]!["key"]!["type"]!.Value<string>().ShouldBe("string");
        header["schema"]!["properties"]!["scope"]!["type"]!.Value<string>().ShouldBe("string");
        header["example"]!["key"]!.Value<string>().ShouldBe("demo-key");
        header["example"]!["scope"]!.Value<string>().ShouldBe("tenant-a");
    }

    [Fact]
    public async Task x402_headers_are_added_to_request_and_responses()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JToken.Parse(json)["paths"]!["/api/test-cases/x402/success"]!["get"]!;

        operation["parameters"]!.SelectToken("$[?(@.name=='PAYMENT-SIGNATURE')].in")!.Value<string>().ShouldBe("header");
        operation["responses"]!["200"]!["headers"]!["PAYMENT-RESPONSE"]!["schema"]!["type"]!.Value<string>().ShouldBe("string");
        operation["responses"]!["402"]!["headers"]!["PAYMENT-REQUIRED"]!["schema"]!["type"]!.Value<string>().ShouldBe("string");
    }

    [Fact]
    public async Task configured_response_header_with_anonymous_example_uses_inline_schema()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var header = JToken.Parse(json)["paths"]!["/api/admin/login"]!["post"]!["responses"]!["200"]!["headers"]!["x-some-custom-header"]!;

        header["schema"]!["$ref"].ShouldBeNull();
        header["schema"]!["type"]!.Value<string>().ShouldBe("object");
        header["schema"]!["properties"]!["prop1"]!["type"]!.Value<string>().ShouldBe("string");
        header["example"]!["prop1"]!.Value<string>().ShouldBe("prop1 val");
    }
}
