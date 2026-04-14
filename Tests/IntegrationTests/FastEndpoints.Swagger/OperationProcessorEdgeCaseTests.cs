namespace Swagger;

public class OperationProcessorEdgeCaseTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task swagger_generation_is_stable_across_multiple_runs()
    {
        var first = await App.DocGenerator.GenerateAsync("Swagger Review");
        var second = await App.DocGenerator.GenerateAsync("Swagger Review");

        JToken.Parse(first.ToJson()).ShouldBeEquivalentTo(JToken.Parse(second.ToJson()));
    }

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

    [Fact]
    public async Task from_body_property_replaces_request_body_schema()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var operation = JToken.Parse(doc.ToJson())["paths"]!["/api/test-cases/from-body-binding/{id}"]!["post"]!;

        operation["requestBody"]!["x-name"]!.Value<string>().ShouldBe("product");
        operation["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>()
                                                                                      .ShouldBe("#/components/schemas/TestCasesFromBodyJsonBindingProduct");
        operation["parameters"]!.SelectToken("$[?(@.name=='customerID')].in")!.Value<string>().ShouldBe("header");
        operation["parameters"]!.SelectToken("$[?(@.name=='id')].in")!.Value<string>().ShouldBe("path");
    }

    [Fact]
    public async Task json_patch_request_body_uses_operations_array_schema()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var schema = JToken.Parse(doc.ToJson())["paths"]!["/api/json-patch-test/{id}"]!["patch"]!["requestBody"]!["content"]!["application/json-patch+json"]!["schema"]!;

        schema["type"]!.Value<string>().ShouldBe("array");
        schema["items"]!["$ref"]!.Value<string>()
                                 .ShouldBe("#/components/schemas/MicrosoftAspNetCoreJsonPatchSystemTextJsonOperationsOperationOfPerson");
    }

    [Fact]
    public async Task idempotency_header_is_added_as_required_parameter()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var header = JToken.Parse(doc.ToJson())["paths"]!["/api/test-cases/idempotency/{id}"]!["get"]!["parameters"]!
                           .First(p => p["name"]!.Value<string>() == "idempotency-Key");

        header["in"]!.Value<string>().ShouldBe("header");
        header["required"]!.Value<bool>().ShouldBeTrue();
        header["schema"]!["type"]!.Value<string>().ShouldBe("string");
    }

    [Fact]
    public async Task x402_headers_are_added_to_request_and_responses()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var operation = JToken.Parse(doc.ToJson())["paths"]!["/api/test-cases/x402/success"]!["get"]!;

        operation["parameters"]!.SelectToken("$[?(@.name=='PAYMENT-SIGNATURE')].in")!.Value<string>().ShouldBe("header");
        operation["responses"]!["200"]!["headers"]!["PAYMENT-RESPONSE"]!["schema"]!["type"]!.Value<string>().ShouldBe("string");
        operation["responses"]!["402"]!["headers"]!["PAYMENT-REQUIRED"]!["schema"]!["type"]!.Value<string>().ShouldBe("string");
    }

    [Fact]
    public async Task typed_result_endpoint_keeps_response_shapes()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var responses = JToken.Parse(doc.ToJson())["paths"]!["/api/multi-test"]!["post"]!["responses"]!;

        responses["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>()
                                                                              .ShouldBe("#/components/schemas/TestCasesTypedResultTestResponse");
        responses["400"]!["content"]!["application/problem+json"]!["schema"]!["$ref"]!.Value<string>()
                                                                                      .ShouldBe("#/components/schemas/FastEndpointsProblemDetails");
        responses["404"]!["description"]!.Value<string>().ShouldBe("Not Found");
    }
}