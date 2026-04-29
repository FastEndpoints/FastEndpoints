using FastEndpoints.OpenApi;

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
    public async Task endpoint_specific_request_metadata_does_not_mutate_shared_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var alphaSchema = doc["paths"]!["/api/swagger-review/shared-request-metadata-alpha"]!["post"]!
                             ["requestBody"]!["content"]!["application/json"]!["schema"]!;
        var betaSchema = doc["paths"]!["/api/swagger-review/shared-request-metadata-beta"]!["post"]!
                            ["requestBody"]!["content"]!["application/json"]!["schema"]!;
        var componentSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedRequestMetadataReviewRequest"];

        alphaSchema["$ref"].ShouldBeNull();
        betaSchema["$ref"].ShouldBeNull();
        alphaSchema["example"]!["name"]!.Value<string>().ShouldBe("alpha example");
        betaSchema["example"]!["name"]!.Value<string>().ShouldBe("beta example");
        alphaSchema["properties"]!["name"]!["description"]!.Value<string>().ShouldBe("alpha description");
        betaSchema["properties"]!["name"]!["description"]!.Value<string>().ShouldBe("beta description");
        componentSchema.ShouldBeNull();
    }

    [Fact]
    public async Task shared_request_schema_ref_initialization_is_thread_safe()
    {
        var sharedCtx = new SharedContext();
        var docOpts = new DocumentOptions
        {
            EndpointFilter = ep => ep.EndpointTags?.Contains("swagger_review") is true
        };
        const string sharedRequestRef = "TestCasesSwaggerReviewSharedRequestMetadataReviewRequest";
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, Environment.ProcessorCount * 8)
                              .Select(_ => Task.Run(
                                          () =>
                                          {
                                              start.Wait();
                                              sharedCtx.InitializeSharedRequestSchemaRefs(App.Services, docOpts);
                                              sharedCtx.SharedRequestSchemaRefs.Contains(sharedRequestRef).ShouldBeTrue();
                                          }))
                              .ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        sharedCtx.SharedRequestSchemaRefs.Contains(sharedRequestRef).ShouldBeTrue();
        sharedCtx.SharedRequestSchemaRefs.ShouldNotBeOfType<HashSet<string>>();
    }

    [Fact]
    public async Task default_version_document_excludes_v1_endpoints_from_schema_sharing()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var initialSchema = doc["paths"]!["/api/swagger-review/version-prefilter-initial"]!["post"]!
                               ["requestBody"]!["content"]!["application/json"]!["schema"]!;

        initialSchema["$ref"]!.Value<string>().ShouldBe("#/components/schemas/TestCasesSwaggerReviewVersionPrefilterSharedRequest");
        initialSchema["properties"].ShouldBeNull();
        doc["paths"]!["/api/swagger-review/version-prefilter-v1"].ShouldBeNull();
    }

    [Fact]
    public async Task illegal_header_names_are_not_added_as_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JToken.Parse(json)["paths"]!["/api/swagger-review/illegal-headers"]!["post"]!["parameters"];

        parameters.ShouldBeNull();
    }

    [Fact]
    public async Task ulong_enum_schema_keeps_values_above_long_max()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var enumValue = JToken.Parse(json)["components"]!["schemas"]!["TestCasesSwaggerReviewUlongEnumReviewStatus"]!["enum"]![0]!;

        enumValue.ToString().ShouldBe("18446744073709551615");
    }

    [Fact]
    public async Task filtered_operation_does_not_remove_other_methods_on_same_path()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var pathItem = JToken.Parse(json)["paths"]!["/api/filtered-shared-path"]!;

        pathItem["get"].ShouldBeNull();
        pathItem["post"].ShouldNotBeNull();
    }

    [Fact]
    public async Task bare_route_stripping_only_removes_structural_segments()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var tags = JToken.Parse(json)["paths"]!["/apiary/ver0/status"]!["get"]!["tags"]!
                         .Values<string>()
                         .ToArray();

        tags.ShouldBe(["Apiary"]);
    }

    [Fact]
    public async Task catch_all_route_parameter_is_normalized_in_path_and_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var operation = doc["paths"]!["/api/swagger-review/catch-all/{slug}"]!["get"]!;
        var pathParam = operation["parameters"]!.First(p => p["in"]!.Value<string>() == "path");

        doc["paths"]!["/api/swagger-review/catch-all/{*slug}"].ShouldBeNull();
        pathParam["name"]!.Value<string>().ShouldBe("slug");
    }

    [Fact]
    public async Task query_parameter_duplicate_detection_uses_naming_policy_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/duplicate-query-naming-policy"]!["get"]!;
        var firstNameParams = operation["parameters"]!
                              .Where(p => p["in"]!.Value<string>() == "query" && p["name"]!.Value<string>() == "firstName")
                              .ToArray();

        firstNameParams.Length.ShouldBe(1);
    }

    [Fact]
    public async Task get_request_uses_bind_from_name_for_query_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/bindfrom-query-get"]!["get"]!;
        var queryParam = operation["parameters"]!.First(p => p["in"]!.Value<string>() == "query");

        queryParam["name"]!.Value<string>().ShouldBe("id");
        operation["parameters"]!.Any(p => p["name"]!.Value<string>() == "customerID").ShouldBeFalse();
    }

    [Fact]
    public async Task non_get_query_param_attribute_with_bind_from_is_added_using_bind_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/bindfrom-query-post"]!["post"]!;
        var queryParam = operation["parameters"]!.First(p => p["in"]!.Value<string>() == "query");

        queryParam["name"]!.Value<string>().ShouldBe("id");
        operation["parameters"]!.Any(p => p["name"]!.Value<string>() == "customerID").ShouldBeFalse();
    }

    [Fact]
    public async Task query_parameter_metadata_uses_binding_name_not_json_property_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/json-named-query-metadata"]!["get"]!;
        var queryParam = operation["parameters"]!.First(p => p["in"]!.Value<string>() == "query");

        queryParam["name"]!.Value<string>().ShouldBe("customerId");
        operation["parameters"]!.Any(p => p["name"]!.Value<string>() == "customer_id").ShouldBeFalse();
        queryParam["description"]!.Value<string>().ShouldBe("customer id query summary");
        queryParam["schema"]!["default"]!.Value<string>().ShouldBe("default-customer");
        queryParam["example"]!.Value<string>().ShouldBe("example-customer");
    }

    [Fact]
    public async Task nullable_query_param_attribute_with_is_required_is_added_as_required_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/required-query-param"]!["post"]!;
        var requiredSearch = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "search");
        var optionalFilter = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "filter");

        requiredSearch["in"]!.Value<string>().ShouldBe("query");
        requiredSearch["required"]!.Value<bool>().ShouldBeTrue();
        optionalFilter["required"].ShouldBeNull();
    }

    [Fact]
    public async Task promoted_body_schema_keeps_validation_rules_from_promoted_property_subtree()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["paths"]!["/api/swagger-review/promoted-body-validation/{id}"]!["post"]!
                                ["requestBody"]!["content"]!["application/json"]!["schema"]!;
        var schema = requestSchema;

        schema["properties"]!["body"].ShouldBeNull();
        schema["properties"]!["name"]!["minLength"]!.Value<int>().ShouldBe(3);
        schema["required"]!.Values<string>().ShouldContain("name");
        schema["properties"]!["child"]!["properties"]!["code"]!["minLength"]!.Value<int>().ShouldBe(2);
        schema["properties"]!["child"]!["required"]!.Values<string>().ShouldContain("code");
    }

    [Fact]
    public async Task promoted_body_request_examples_are_unwrapped_to_promoted_schema_shape()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/promoted-body-validation/{id}"]!["post"]!;
        var example = operation["requestBody"]!["content"]!["application/json"]!["example"]!;

        example["body"].ShouldBeNull();
        example["id"].ShouldBeNull();
        example["name"]!.Value<string>().ShouldBe("example name");
        example["child"]!["code"]!.Value<string>().ShouldBe("xy");
    }

    [Fact]
    public async Task get_request_from_cookie_property_is_not_duplicated_as_query_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JToken.Parse(json)["paths"]!["/api/swagger-review/cookie-get"]!["get"]!["parameters"]!;

        parameters.Count(p => p["in"]!.Value<string>() == "cookie" && p["name"]!.Value<string>() == "session_id").ShouldBe(1);
        parameters.Any(p => p["in"]!.Value<string>() == "query" && p["name"]!.Value<string>() == "sessionId").ShouldBeFalse();
        parameters.Any(p => p["in"]!.Value<string>() == "query" && p["name"]!.Value<string>() == "SessionId").ShouldBeFalse();
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

        var schema = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;

        schema["$ref"].ShouldBeNull();
        schema["properties"]!["id"].ShouldNotBeNull();
        schema["properties"]!["name"].ShouldNotBeNull();
        schema["properties"]!["price"].ShouldNotBeNull();
        schema["properties"]!["price"]!["exclusiveMinimum"]!.Value<int>().ShouldBe(200);
        schema["example"]!["name"]!.Value<string>().ShouldBe("test product name");
        operation["parameters"]!.SelectToken("$[?(@.name=='customerID')].in")!.Value<string>().ShouldBe("header");
        operation["parameters"]!.SelectToken("$[?(@.name=='id')].in")!.Value<string>().ShouldBe("path");
    }

    [Fact]
    public async Task promoted_body_validation_and_examples_do_not_mutate_shared_response_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var productSchema = JToken.Parse(json)["components"]!["schemas"]!["TestCasesFromBodyJsonBindingProduct"]!;

        productSchema["properties"]!["price"]!["exclusiveMinimum"].ShouldBeNull();
        productSchema["example"].ShouldBeNull();
        productSchema["properties"]!["name"]!["example"].ShouldBeNull();
        productSchema["properties"]!["id"]!["description"]!.Value<string>().ShouldBe("product id goes here");
    }

    [Fact]
    public async Task json_patch_request_body_uses_json_patch_document_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var content = JToken.Parse(json)["paths"]!["/api/json-patch-test/{id}"]!["patch"]!["requestBody"]!["content"]!;

        // MS OpenApi generates a proper JsonPatchDocument<T> schema using the framework's built-in type
        // instead of NSwag's incorrect type:object. see accepted differences in the port review.
        var schema = content["application/json-patch+json"]!["schema"]!;
        schema["type"]!.Value<string>().ShouldBe("array");
        schema["items"]!["oneOf"]!.ShouldNotBeNull();
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
    public async Task idempotency_header_is_not_duplicated_when_already_present()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JToken.Parse(json)["paths"]!["/api/swagger-review/duplicate-idempotency-header"]!["post"]!["parameters"]!;

        parameters.Count(p => p["in"]!.Value<string>() == "header" && p["name"]!.Value<string>() == "Idempotency-Key").ShouldBe(1);
    }

    [Fact]
    public async Task x402_signature_header_is_not_duplicated_when_already_present()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JToken.Parse(json)["paths"]!["/api/swagger-review/duplicate-x402-header"]!["get"]!["parameters"]!;

        parameters.Count(p => p["in"]!.Value<string>() == "header" && p["name"]!.Value<string>() == "PAYMENT-SIGNATURE").ShouldBe(1);
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

    [Fact]
    public async Task request_examples_do_not_keep_null_for_non_nullable_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var examples = JToken.Parse(json)["paths"]!["/api/inventory/manage/create"]!["post"]!["requestBody"]!["content"]!["application/json"]!["examples"]!;

        examples["Example 1"]!["value"]!["modifiedBy"]!.Value<string>().ShouldBe("modifiedBy");
        examples["Example 2"]!["value"]!["modifiedBy"]!.Value<string>().ShouldBe("modifiedBy");
    }

    [Fact]
    public async Task dictionary_query_parameter_uses_object_schema_not_missing_keyvaluepair_ref()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var operation = JToken.Parse(json)["paths"]!["/api/test-cases/json-array-binding-for-ienumerable-props"]!["get"]!;
        var dictParam = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "dict");

        dictParam["schema"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["$ref"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["type"]!.Value<string>().ShouldBe("object");
        dictParam["content"]!["application/json"]!["schema"]!["additionalProperties"]!["type"]!.Value<string>().ShouldBe("string");
        var responseSchema = operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;

        responseSchema["$ref"]!.Value<string>().ShouldBe("#/components/schemas/TestCasesJsonArrayBindingForIEnumerablePropsResponse");
        responseSchema.ToString().ShouldNotContain("SystemCollectionsGenericKeyValuePairOfStringAndString");
    }

    [Fact]
    public async Task complex_query_object_parameter_uses_json_content_unless_from_query()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JToken.Parse(json);
        var operation = doc["paths"]!["/api/test-cases/json-array-binding-for-ienumerable-props"]!["get"]!;
        var stevenParam = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "steven");

        stevenParam["schema"].ShouldBeNull();
        stevenParam["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>()
                  .ShouldBe("#/components/schemas/TestCasesJsonArrayBindingForIEnumerablePropsRequest_Person");

        var fromQueryOperation = doc["paths"]!["/api/test-cases/query-param-creation-from-test-helpers/{complexId}/{complexIdString}"]!["get"]!;
        var fromQueryParameters = fromQueryOperation["parameters"]!;

        fromQueryParameters.Any(p => p["name"]!.Value<string>() == "Nested").ShouldBeFalse();
        fromQueryParameters.Any(p => p["name"]!.Value<string>() == "first" && p["in"]!.Value<string>() == "query").ShouldBeTrue();
        fromQueryParameters.Any(p => p["name"]!.Value<string>() == "last" && p["in"]!.Value<string>() == "query").ShouldBeTrue();
        doc["components"]!["schemas"]!["TestCasesHydratedQueryParamGeneratorTestRequest_NestedClass"].ShouldBeNull();
    }

    [Fact]
    public async Task child_validator_rules_are_applied_to_operation_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewChildValidatorReviewRequest"]!;
        var childSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewChildValidatorReviewChild"]!;

        requestSchema["properties"]!["child"]!["properties"]!["score"]!["exclusiveMinimum"]!.Value<int>().ShouldBe(10);
        childSchema.ShouldBeNull();
    }

    [Fact]
    public async Task deep_nested_validator_rules_are_applied_to_operation_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewDeepNestedValidatorReviewRequest"]!;
        var grandChildSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewDeepNestedValidatorReviewGrandChild"]!;

        requestSchema["properties"]!["child"]!["properties"]!["subChild"]!["properties"]!["field"]!["minLength"]!.Value<int>().ShouldBe(5);
        grandChildSchema.ShouldBeNull();
    }

    [Fact]
    public async Task parent_path_validator_rules_do_not_mutate_shared_nested_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var alphaRequest = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationAlphaRequest"]!;
        var betaRequest = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationBetaRequest"]!;
        var addressComponent = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationAddress"]!;

        alphaRequest["properties"]!["address"]!["$ref"].ShouldBeNull();
        alphaRequest["properties"]!["address"]!["required"]!.Values<string>().ShouldContain("zip");
        betaRequest["properties"]!["address"]!["$ref"]!.Value<string>()
                   .ShouldBe("#/components/schemas/TestCasesSwaggerReviewSharedNestedValidationAddress");
        addressComponent["required"].ShouldBeNull();
    }

    [Fact]
    public async Task validator_rules_are_applied_through_intermediate_non_generic_base_type()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var requestSchema = JToken.Parse(json)["components"]!["schemas"]!["TestCasesSwaggerReviewIntermediateBaseValidatorReviewRequest"]!;

        requestSchema["properties"]!["name"]!["minLength"]!.Value<int>().ShouldBe(3);
    }

    [Fact]
    public async Task included_validator_rules_are_applied_to_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var requestSchema = JToken.Parse(json)["components"]!["schemas"]!["TestCasesIncludedValidatorTestRequest"]!;

        requestSchema["required"]!.Values<string>().ShouldContain("id");
        requestSchema["properties"]!["id"]!["exclusiveMinimum"]!.Value<string>().ShouldBe("5");
        requestSchema["properties"]!["name"]!["minLength"]!.Value<int>().ShouldBe(5);
    }

    [Fact]
    public async Task json_property_name_attributes_are_used_by_to_header_transformer()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewJsonPropertyNameTransformerReviewResponse"]!;

        responseSchema["properties"]!["x_secret"].ShouldBeNull();
        responseSchema["properties"]!["bodyValue"].ShouldNotBeNull();
    }

    [Fact]
    public async Task interface_dictionary_query_parameter_uses_object_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/interface-dictionary"]!["get"]!;
        var dictParam = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "metadata");

        dictParam["schema"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["type"]!.Value<string>().ShouldBe("object");
        dictParam["content"]!["application/json"]!["schema"]!["additionalProperties"]!["type"]!.Value<string>().ShouldBe("string");
    }

    [Fact]
    public async Task manually_added_complex_parameter_and_header_refs_have_component_schemas()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var nestedRef = "#/components/schemas/TestCasesSwaggerReviewManualSchemaNested";
        var idempotencyRef = "#/components/schemas/TestCasesSwaggerReviewManualSchemaIdempotencyHeader";
        var queryParam = doc["paths"]!["/api/swagger-review/manual-complex-query"]!["get"]!["parameters"]!
                            .First(p => p["name"]!.Value<string>() == "filter");
        var responseHeader = doc["paths"]!["/api/swagger-review/manual-complex-response-header"]!["get"]!["responses"]!["200"]!
                                ["headers"]!["x-complex-header"]!;
        var idempotencyHeader = doc["paths"]!["/api/swagger-review/manual-complex-idempotency-header"]!["post"]!["parameters"]!
                                  .First(p => p["name"]!.Value<string>() == "Idempotency-Key");

        queryParam["content"]!["application/json"]!["schema"]!["$ref"]!.Value<string>().ShouldBe(nestedRef);
        responseHeader["schema"]!["$ref"]!.Value<string>().ShouldBe(nestedRef);
        idempotencyHeader["schema"]!["$ref"]!.Value<string>().ShouldBe(idempotencyRef);
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewManualSchemaNested"].ShouldNotBeNull();
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewManualSchemaIdempotencyHeader"].ShouldNotBeNull();
    }

    [Fact]
    public async Task xml_docs_are_applied_for_properties_on_generic_types()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewGenericXmlDocReviewRequest"]!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewGenericXmlDocReviewResponse"]!;

        requestSchema["properties"]!["value"]!["description"]!.Value<string>().ShouldBe("wrapped value summary");
        requestSchema["properties"]!["value"]!["example"]!.Value<string>().ShouldBe("wrapped example");
        responseSchema["description"]!.Value<string>().ShouldBe("generic review response summary");
        responseSchema["properties"]!["value"]!["description"]!.Value<string>().ShouldBe("wrapped value summary");
    }

    [Fact]
    public async Task xml_doc_inline_markup_text_is_preserved_in_descriptions()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewInlineMarkupXmlDocReviewRequest"]!;

        requestSchema["description"]!.Value<string>().ShouldBe("returns the User record.");
        requestSchema["properties"]!["userId"]!["description"]!.Value<string>().ShouldBe("filter by UserId value.");
    }

    [Fact]
    public async Task missing_schema_generation_uses_primitive_formats_for_primitive_like_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewMissingSchemaPrimitiveResponse"]!;

        responseSchema["properties"]!["correlationId"]!["type"]!.Value<string>().ShouldBe("string");
        responseSchema["properties"]!["correlationId"]!["format"]!.Value<string>().ShouldBe("uuid");
        responseSchema["properties"]!["effectiveOn"]!["$ref"]!.Value<string>().ShouldBe("#/components/schemas/SystemDateOnly");
        doc["components"]!["schemas"]!["SystemDateOnly"]!["type"]!.Value<string>().ShouldBe("string");
        doc["components"]!["schemas"]!["SystemDateOnly"]!["format"]!.Value<string>().ShouldBe("date");
    }

    [Fact]
    public async Task missing_schema_generation_runs_schema_transformers()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewMissingSchemaEnumResponse"]!;
        var enumSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUlongEnumReviewStatus"]!;

        responseSchema["properties"]!["status"]!["$ref"]!.Value<string>().ShouldBe("#/components/schemas/TestCasesSwaggerReviewUlongEnumReviewStatus");
        enumSchema["enum"]![0]!.ToString().ShouldBe("18446744073709551615");
    }

    [Fact]
    public async Task orphan_constrained_route_param_uses_constraint_type()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JToken.Parse(json)["paths"]!["/api/test-cases/ep-witout-req-route-binding-test/{customerID}/{otherID}"]!["get"]!;
        var customerId = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "customerID");

        customerId["schema"]!["type"]!.Value<string>().ShouldBe("integer");
        customerId["schema"]!["format"]!.Value<string>().ShouldBe("int32");
    }

    [Fact]
    public async Task endpoint_without_request_does_not_use_response_metadata_for_request_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/no-request-metadata-leak/{leakId}"]!["get"]!;
        var leakId = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "leakId");

        leakId["description"].ShouldBeNull();
    }

    [Fact]
    public async Task inline_default_route_values_are_removed_from_openapi_paths_and_parameter_names()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/default-route-value/{id}"]!["get"]!;
        var id = operation["parameters"]!.First(p => p["name"]!.Value<string>() == "id");

        id["description"]!.Value<string>().ShouldBe("route param summary");
    }

    [Fact]
    public async Task multi_route_endpoint_uses_path_parameters_from_matching_route_only()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JToken.Parse(json);
        var saveOperation = doc["paths"]!["/api/customer/save"]!["get"]!;
        var pathParams = saveOperation["parameters"]!
                                      .Where(p => p["in"]!.Value<string>() == "path")
                                      .Select(p => p["name"]!.Value<string>())
                                      .ToArray();
        var routedOperation = doc["paths"]!["/api/customer/{cID}/new/{sourceID}"]!["get"]!;
        var routedParams = routedOperation["parameters"]!
                                          .Select(p => new
                                          {
                                              Name = p["name"]!.Value<string>(),
                                              Location = p["in"]!.Value<string>()
                                          })
                                          .ToArray();

        pathParams.ShouldBeEmpty();
        routedParams.ShouldContain(p => p.Name == "cID" && p.Location == "path");
        routedParams.ShouldContain(p => p.Name == "sourceID" && p.Location == "path");
        routedParams.ShouldNotContain(p => p.Name == "refererID");
    }

}
