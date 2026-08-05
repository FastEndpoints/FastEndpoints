namespace OpenApi;

public class OperationTransformerEdgeCaseTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task swagger_generation_is_stable_across_multiple_runs()
    {
        var first = await App.GetDocumentJsonAsync("Swagger Review");
        var second = await App.GetDocumentJsonAsync("Swagger Review");

        JsonNode.DeepEquals(JsonNode.Parse(first)!, JsonNode.Parse(second)!).ShouldBeTrue();
    }

    [Fact]
    public async Task query_method_endpoint_is_omitted_when_openapi_tooling_drops_unknown_method()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;

        doc["paths"]!["/api/swagger-review/query-method"].ShouldBeNull();
    }

    [Fact]
    public async Task auto_tag_override_uses_override_value()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var tags = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/auto-tag-override"]!["get"]!["tags"]!
                         .StringValues()
                         .ToArray();

        tags.ShouldBe(["ReviewTag"]);
    }

    [Fact]
    public async Task duplicate_request_example_labels_are_indexed()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var examples = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/duplicate-examples"]!["post"]!
            ["requestBody"]!["content"]!["application/json"]!["examples"]!.AsObject();

        examples.Select(p => p.Key).ToArray().ShouldBe(["Example 1", "Example 2"]);
    }

    [Fact]
    public async Task endpoint_specific_request_metadata_does_not_mutate_shared_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var alphaSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-request-metadata-alpha"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var betaSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-request-metadata-beta"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var componentSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedRequestMetadataReviewRequest"];

        alphaSchema["example"]!["name"]!.GetValue<string>().ShouldBe("alpha example");
        betaSchema["example"]!["name"]!.GetValue<string>().ShouldBe("beta example");
        alphaSchema["properties"]!["name"]!["description"]!.GetValue<string>().ShouldBe("alpha description");
        betaSchema["properties"]!["name"]!["description"]!.GetValue<string>().ShouldBe("beta description");
        componentSchema.ShouldBeNull();
    }

    [Fact]
    public async Task default_version_document_excludes_v1_endpoints_from_schema_sharing()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var initialSchema = doc["paths"]!["/api/swagger-review/version-prefilter-initial"]!["post"]!
            ["requestBody"]!["content"]!["application/json"]!["schema"]!;

        initialSchema["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/TestCasesSwaggerReviewVersionPrefilterSharedRequest");
        initialSchema["properties"].ShouldBeNull();
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewVersionPrefilterSharedRequest"]!["properties"]!["name"]!["description"]!
            .GetValue<string>()
            .ShouldBe("initial description");
        doc["components"]!["schemas"]!.AsObject().Any(p => p.Key.StartsWith("TestCasesSwaggerReviewVersionPrefilterSharedRequest__op", StringComparison.Ordinal))
                                                 .ShouldBeFalse();
        doc["paths"]!["/api/swagger-review/version-prefilter-v1"].ShouldBeNull();
    }

    [Fact]
    public async Task illegal_header_names_are_not_added_as_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/illegal-headers"]!["post"]!["parameters"];

        parameters.ShouldBeNull();
    }

    [Fact]
    public async Task ulong_enum_schema_keeps_values_above_long_max()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var enumValue = JsonNode.Parse(json)!["components"]!["schemas"]!["TestCasesSwaggerReviewUlongEnumReviewStatus"]!["enum"]![0]!;

        enumValue.GetValue<string>().ShouldBe("Max");
    }

    [Fact]
    public async Task filtered_operation_does_not_remove_other_methods_on_same_path()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var pathItem = JsonNode.Parse(json)!["paths"]!["/api/filtered-shared-path"]!;

        pathItem["get"].ShouldBeNull();
        pathItem["post"].ShouldNotBeNull();
    }

    [Fact]
    public async Task bare_route_stripping_only_removes_structural_segments()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var tags = JsonNode.Parse(json)!["paths"]!["/apiary/ver0/status"]!["get"]!["tags"]!
                         .StringValues()
                         .ToArray();

        tags.ShouldBe(["Apiary"]);
    }

    [Fact]
    public async Task catch_all_route_parameter_is_normalized_in_path_and_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var operation = doc["paths"]!["/api/swagger-review/catch-all/{slug}"]!["get"]!;
        var pathParam = operation["parameters"]!.AsArray().First(p => p["in"]!.GetValue<string>() == "path");

        doc["paths"]!["/api/swagger-review/catch-all/{*slug}"].ShouldBeNull();
        pathParam["name"]!.GetValue<string>().ShouldBe("slug");
    }

    [Fact]
    public async Task query_parameter_duplicate_detection_uses_naming_policy_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/duplicate-query-naming-policy"]!["get"]!;
        var firstNameParams = operation["parameters"]!.AsArray()
                              .Where(p => p["in"]!.GetValue<string>() == "query" && p["name"]!.GetValue<string>() == "firstName")
                              .ToArray();

        firstNameParams.Length.ShouldBe(1);
    }

    [Fact]
    public async Task get_request_uses_bind_from_name_for_query_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/bindfrom-query-get"]!["get"]!;
        var queryParam = operation["parameters"]!.AsArray().First(p => p["in"]!.GetValue<string>() == "query");

        queryParam["name"]!.GetValue<string>().ShouldBe("id");
        operation["parameters"]!.AsArray().Any(p => p["name"]!.GetValue<string>() == "customerID").ShouldBeFalse();
    }

    [Fact]
    public async Task non_get_query_param_attribute_with_bind_from_is_added_using_bind_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/bindfrom-query-post"]!["post"]!;
        var queryParam = operation["parameters"]!.AsArray().First(p => p["in"]!.GetValue<string>() == "query");

        queryParam["name"]!.GetValue<string>().ShouldBe("id");
        operation["parameters"]!.AsArray().Any(p => p["name"]!.GetValue<string>() == "customerID").ShouldBeFalse();
    }

    [Fact]
    public async Task query_parameter_metadata_uses_binding_name_not_json_property_name()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/json-named-query-metadata"]!["get"]!;
        var queryParam = operation["parameters"]!.AsArray().First(p => p["in"]!.GetValue<string>() == "query");

        queryParam["name"]!.GetValue<string>().ShouldBe("customerId");
        operation["parameters"]!.AsArray().Any(p => p["name"]!.GetValue<string>() == "customer_id").ShouldBeFalse();
        queryParam["description"]!.GetValue<string>().ShouldBe("customer id query summary");
        queryParam["schema"]!["default"]!.GetValue<string>().ShouldBe("default-customer");
        queryParam["example"]!.GetValue<string>().ShouldBe("example-customer");
    }

    [Fact]
    public async Task default_value_attributes_are_applied_to_request_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/default-value-schema"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

        requestSchema["properties"]!["name"]!["default"]!.GetValue<string>().ShouldBe("schema-default");
        requestSchema["properties"]!["count"]!["default"]!.GetValue<int>().ShouldBe(7);
    }

    [Fact]
    public async Task nullable_query_param_attribute_with_is_required_is_added_as_required_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/required-query-param"]!["post"]!;
        var requiredSearch = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "search");
        var optionalFilter = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "filter");

        requiredSearch["in"]!.GetValue<string>().ShouldBe("query");
        requiredSearch["required"]!.GetValue<bool>().ShouldBeTrue();
        optionalFilter["required"].ShouldBeNull();

        var requiredSearchType = requiredSearch["schema"]!["type"]!;
        requiredSearchType.ShouldBeOfType<JsonArray>();
        requiredSearchType.StringValues().ShouldContain("string");
        requiredSearchType.StringValues().ShouldContain("null");

        var optionalFilterType = optionalFilter["schema"]!["type"]!;
        optionalFilterType.ShouldBeOfType<JsonArray>();
        optionalFilterType.StringValues().ShouldContain("string");
        optionalFilterType.StringValues().ShouldContain("null");
    }

    [Fact]
    public async Task unique_items_are_emitted_for_scalar_set_types_and_explicit_opt_in()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUniqueItemsReviewRequest"]!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUniqueItemsReviewResponse"]!;
        var hashSetStringSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericHashSetOfSystemString"]!;
        var hashSetChildSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericHashSetOfTestCasesSwaggerReviewUniqueItemsReviewChild"]!;
        var listChildSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild"]!;
        var sortedSetIntSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericSortedSetOfSystemInt32"]!;

        requestSchema["properties"]!["autoTags"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/SystemCollectionsGenericHashSetOfSystemString");
        requestSchema["properties"]!["autoChildren"]!["$ref"]!.GetValue<string>()
                                                              .ShouldBe("#/components/schemas/SystemCollectionsGenericHashSetOfTestCasesSwaggerReviewUniqueItemsReviewChild");
        requestSchema["properties"]!["explicitChildren"]!["$ref"]!.GetValue<string>()
                                                                  .ShouldBe("#/components/schemas/SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild");

        hashSetStringSchema["uniqueItems"]!.GetValue<bool>().ShouldBeTrue();
        hashSetChildSchema["uniqueItems"].ShouldBeNull();
        listChildSchema["uniqueItems"]!.GetValue<bool>().ShouldBeTrue();
        sortedSetIntSchema["uniqueItems"]!.GetValue<bool>().ShouldBeTrue();

        responseSchema["properties"]!["autoIds"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/SystemCollectionsGenericSortedSetOfSystemInt32");
        responseSchema["properties"]!["explicitChildren"]!["$ref"]!.GetValue<string>()
                                                                   .ShouldBe("#/components/schemas/SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild");
    }

    [Fact]
    public async Task promoted_body_schema_keeps_validation_rules_from_promoted_property_subtree()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/promoted-body-validation/{id}"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = ResolveSchema(doc, requestSchema["properties"]!["child"]!);

        requestSchema["properties"]!["body"].ShouldBeNull();
        requestSchema["properties"]!["name"]!["minLength"]!.GetValue<int>().ShouldBe(3);
        requestSchema["required"]!.StringValues().ShouldContain("name");
        requestSchema["properties"]!["child"]!["$ref"]!.GetValue<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewPromotedBodyValidationChild");
        childSchema["properties"]!["code"]!["minLength"]!.GetValue<int>().ShouldBe(2);
        childSchema["required"]!.StringValues().ShouldContain("code");
    }

    [Fact]
    public async Task promoted_body_request_examples_are_unwrapped_to_promoted_schema_shape()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/promoted-body-validation/{id}"]!["post"]!;
        var example = operation["requestBody"]!["content"]!["application/json"]!["example"]!;

        example["body"].ShouldBeNull();
        example["id"].ShouldBeNull();
        example["name"]!.GetValue<string>().ShouldBe("example name");
        example["child"]!["code"]!.GetValue<string>().ShouldBe("xy");
    }

    [Fact]
    public async Task get_request_from_cookie_property_is_not_duplicated_as_query_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/cookie-get"]!["get"]!["parameters"]!.AsArray();

        parameters.Count(p => p["in"]!.GetValue<string>() == "cookie" && p["name"]!.GetValue<string>() == "session_id").ShouldBe(1);
        parameters.Any(p => p["in"]!.GetValue<string>() == "query" && p["name"]!.GetValue<string>() == "sessionId").ShouldBeFalse();
        parameters.Any(p => p["in"]!.GetValue<string>() == "query" && p["name"]!.GetValue<string>() == "SessionId").ShouldBeFalse();
    }

    [Fact]
    public async Task empty_request_schemas_are_removed_when_option_is_enabled()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review Empty Schema");
        var doc = JsonNode.Parse(json)!;
        var operation = doc["paths"]!["/api/swagger-review/empty-schema-cleanup"]!["post"]!;

        operation["requestBody"].ShouldBeNull();
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewEmptySchemaCleanupRequest"].ShouldBeNull();
    }

    [Fact]
    public async Task hide_from_docs_properties_are_removed_from_request_and_response_schemas()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/hidden-schema"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewHiddenSchemaReviewResponse"]!;

        requestSchema["properties"]!["visibleValue"].ShouldNotBeNull();
        requestSchema["properties"]!["hiddenValue"].ShouldBeNull();
        requestSchema["properties"]!["ignoredValue"].ShouldBeNull();
        responseSchema["properties"]!["visibleValue"].ShouldNotBeNull();
        responseSchema["properties"]!["hiddenValue"].ShouldBeNull();
    }

    [Fact]
    public async Task from_body_property_replaces_request_body_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/test-cases/from-body-binding/{id}"]!["post"]!;

        var schema = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;

        schema = ResolveSchema(JsonNode.Parse(json)!, schema);
        schema["properties"]!["id"].ShouldNotBeNull();
        schema["properties"]!["name"].ShouldNotBeNull();
        schema["properties"]!["price"].ShouldNotBeNull();
        schema["properties"]!["price"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(200);
        schema["example"]!["name"]!.GetValue<string>().ShouldBe("test product name");
        operation["parameters"]!.AsArray().First(p => p!["name"]!.GetValue<string>() == "customerID")!["in"]!.GetValue<string>().ShouldBe("header");
        operation["parameters"]!.AsArray().First(p => p!["name"]!.GetValue<string>() == "id")!["in"]!.GetValue<string>().ShouldBe("path");
    }

    [Fact]
    public async Task promoted_body_validation_and_examples_do_not_mutate_shared_response_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var productSchema = JsonNode.Parse(json)!["components"]!["schemas"]!["TestCasesFromBodyJsonBindingProduct"]!;

        productSchema["properties"]!["price"]!["exclusiveMinimum"].ShouldBeNull();
        productSchema["example"].ShouldBeNull();
        productSchema["properties"]!["name"]!["example"].ShouldBeNull();
        productSchema["properties"]!["id"]!["description"]!.GetValue<string>().ShouldBe("product id goes here");
    }

    [Fact]
    public async Task json_patch_request_body_uses_json_patch_document_schema()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var content = JsonNode.Parse(json)!["paths"]!["/api/json-patch-test/{id}"]!["patch"]!["requestBody"]!["content"]!;

        // MS OpenApi generates a proper JsonPatchDocument<T> schema using the framework's built-in type
        // instead of NSwag's incorrect type:object. see accepted differences in the port review.
        var schema = content["application/json-patch+json"]!["schema"]!;
        schema = ResolveSchema(JsonNode.Parse(json)!, schema);
        schema["type"]!.GetValue<string>().ShouldBe("array");
        schema["items"]!["oneOf"]!.ShouldNotBeNull();
    }

    [Fact]
    public async Task typed_result_endpoint_keeps_response_shapes()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var responses = JsonNode.Parse(json)!["paths"]!["/api/multi-test"]!["post"]!["responses"]!;

        responses["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>()
                                                                              .ShouldBe("#/components/schemas/TestCasesTypedResultTestResponse");
        responses["400"]!["content"]!["application/problem+json"]!["schema"]!["$ref"]!.GetValue<string>()
                                                                                      .ShouldBe("#/components/schemas/FastEndpointsProblemDetails");
        responses["404"]!["description"]!.GetValue<string>().ShouldBe("Not Found");
    }

    [Fact]
    public async Task idempotency_header_is_added_as_required_parameter()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var header = JsonNode.Parse(json)!["paths"]!["/api/test-cases/idempotency/{id}"]!["get"]!["parameters"]!.AsArray()
                           .First(p => p["name"]!.GetValue<string>() == "Idempotency-Key");

        header["in"]!.GetValue<string>().ShouldBe("header");
        header["required"]!.GetValue<bool>().ShouldBeTrue();
        header["schema"]!["type"]!.GetValue<string>().ShouldBe("string");
    }

    [Fact]
    public async Task idempotency_header_without_explicit_type_uses_example_shape()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var header = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/idempotency-anonymous-example"]!["post"]!["parameters"]!.AsArray()
                           .First(p => p["name"]!.GetValue<string>() == "Idempotency-Key");

        header["description"]!.GetValue<string>().ShouldBe("custom idempotency header");
        header["schema"]!["$ref"].ShouldBeNull();
        header["schema"]!["type"]!.GetValue<string>().ShouldBe("object");
        header["schema"]!["properties"]!["key"]!["type"]!.GetValue<string>().ShouldBe("string");
        header["schema"]!["properties"]!["scope"]!["type"]!.GetValue<string>().ShouldBe("string");
        header["example"]!["key"]!.GetValue<string>().ShouldBe("demo-key");
        header["example"]!["scope"]!.GetValue<string>().ShouldBe("tenant-a");
    }

    [Fact]
    public async Task idempotency_header_is_not_duplicated_when_already_present()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/duplicate-idempotency-header"]!["post"]!["parameters"]!.AsArray();

        parameters.Count(p => p["in"]!.GetValue<string>() == "header" && p["name"]!.GetValue<string>() == "Idempotency-Key").ShouldBe(1);
    }

    [Fact]
    public async Task x402_signature_header_is_not_duplicated_when_already_present()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var parameters = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/duplicate-x402-header"]!["get"]!["parameters"]!.AsArray();

        parameters.Count(p => p["in"]!.GetValue<string>() == "header" && p["name"]!.GetValue<string>() == "PAYMENT-SIGNATURE").ShouldBe(1);
    }

    [Fact]
    public async Task x402_headers_are_added_to_request_and_responses()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/test-cases/x402/success"]!["get"]!;

        operation["parameters"]!.AsArray().First(p => p!["name"]!.GetValue<string>() == "PAYMENT-SIGNATURE")!["in"]!.GetValue<string>().ShouldBe("header");
        operation["responses"]!["200"]!["headers"]!["PAYMENT-RESPONSE"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("string");
        operation["responses"]!["402"]!["headers"]!["PAYMENT-REQUIRED"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("string");
    }

    [Fact]
    public async Task configured_response_header_with_anonymous_example_uses_inline_schema()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var header = JsonNode.Parse(json)!["paths"]!["/api/admin/login"]!["post"]!["responses"]!["200"]!["headers"]!["x-some-custom-header"]!;

        header["schema"]!["$ref"].ShouldBeNull();
        header["schema"]!["type"]!.GetValue<string>().ShouldBe("object");
        header["schema"]!["properties"]!["prop1"]!["type"]!.GetValue<string>().ShouldBe("string");
        header["example"]!["prop1"]!.GetValue<string>().ShouldBe("prop1 val");
    }

    [Fact]
    public async Task request_examples_do_not_keep_null_for_non_nullable_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var examples = JsonNode.Parse(json)!["paths"]!["/api/inventory/manage/create"]!["post"]!["requestBody"]!["content"]!["application/json"]!["examples"]!;

        examples["Example 1"]!["value"]!["modifiedBy"]!.GetValue<string>().ShouldBe("modifiedBy");
        examples["Example 2"]!["value"]!["modifiedBy"]!.GetValue<string>().ShouldBe("modifiedBy");
    }

    [Fact]
    public async Task dictionary_query_parameter_uses_object_schema_not_missing_keyvaluepair_ref()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/test-cases/json-array-binding-for-ienumerable-props"]!["get"]!;
        var dictParam = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "dict");

        dictParam["schema"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["$ref"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("object");
        dictParam["content"]!["application/json"]!["schema"]!["additionalProperties"]!["type"]!.GetValue<string>().ShouldBe("string");
        var responseSchema = operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;

        responseSchema["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/TestCasesJsonArrayBindingForIEnumerablePropsResponse");
        responseSchema.ToString().ShouldNotContain("SystemCollectionsGenericKeyValuePairOfStringAndString");
    }

    [Fact]
    public async Task complex_query_object_parameter_uses_json_content_unless_from_query()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JsonNode.Parse(json)!;
        var operation = doc["paths"]!["/api/test-cases/json-array-binding-for-ienumerable-props"]!["get"]!;
        var stevenParam = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "steven");

        stevenParam["schema"].ShouldBeNull();
        stevenParam["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>()
                                                                        .ShouldBe("#/components/schemas/TestCasesJsonArrayBindingForIEnumerablePropsRequest_Person");

        var fromQueryOperation = doc["paths"]!["/api/test-cases/query-param-creation-from-test-helpers/{complexId}/{complexIdString}"]!["get"]!;
        var fromQueryParameters = fromQueryOperation["parameters"]!.AsArray();

        fromQueryParameters.Any(p => p["name"]!.GetValue<string>() == "Nested").ShouldBeFalse();
        fromQueryParameters.Any(p => p["name"]!.GetValue<string>() == "first" && p["in"]!.GetValue<string>() == "query").ShouldBeTrue();
        fromQueryParameters.Any(p => p["name"]!.GetValue<string>() == "last" && p["in"]!.GetValue<string>() == "query").ShouldBeTrue();
        doc["components"]!["schemas"]!["TestCasesHydratedQueryParamGeneratorTestRequest_NestedClass"].ShouldBeNull();
    }

    [Fact]
    public async Task child_validator_rules_are_applied_to_operation_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/child-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = ResolveSchema(doc, requestSchema["properties"]!["child"]!);

        requestSchema["properties"]!["child"]!["$ref"]!.GetValue<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewChildValidatorReviewChild");
        childSchema["properties"]!["score"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(10);
    }

    [Fact]
    public async Task deep_nested_validator_rules_are_applied_to_operation_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/deep-nested-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewDeepNestedValidatorReviewChild"]!;
        var grandChildSchema = ResolveSchema(doc, childSchema["properties"]!["subChild"]!);

        ResolveSchema(doc, requestSchema["properties"]!["child"]!).ShouldBe(childSchema);
        childSchema["properties"]!["subChild"]!["$ref"]!.GetValue<string>()
                                                        .ShouldBe("#/components/schemas/TestCasesSwaggerReviewDeepNestedValidatorReviewGrandChild");
        grandChildSchema["properties"]!["field"]!["minLength"]!.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public async Task parent_path_validator_rules_do_not_mutate_shared_nested_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var alphaRequest = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-nested-validation-alpha"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var betaRequest = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationBetaRequest"]!;
        var addressComponent = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationAddress"]!;

        var alphaAddress = ResolveSchema(doc, alphaRequest["properties"]!["address"]!);
        alphaAddress["required"]!.StringValues().ShouldContain("zip");
        betaRequest["properties"]!["address"]!["$ref"]!.GetValue<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewSharedNestedValidationAddress");
        addressComponent["required"].ShouldBeNull();
    }

    [Fact]
    public async Task validator_rules_are_applied_through_intermediate_non_generic_base_type()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/intermediate-base-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

        requestSchema["properties"]!["name"]!["minLength"]!.GetValue<int>().ShouldBe(3);
    }

    [Fact]
    public async Task included_validator_rules_are_applied_to_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/test-cases/included-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

        requestSchema["required"]!.StringValues().ShouldContain("id");
        requestSchema["properties"]!["id"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(5);
        requestSchema["properties"]!["name"]!["minLength"]!.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public async Task json_property_name_attributes_are_used_by_to_header_transformer()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewJsonPropertyNameTransformerReviewResponse"]!;

        responseSchema["properties"]!["x_secret"].ShouldBeNull();
        responseSchema["properties"]!["bodyValue"].ShouldNotBeNull();
    }

    [Fact]
    public async Task to_header_response_properties_use_xml_docs_for_description_and_example()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var header = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/json-property-name-transformers"]!["post"]!
            ["responses"]!["200"]!["headers"]!["x-secret"]!;

        header["description"]!.GetValue<string>().ShouldBe("secret header summary");
        header["example"]!.GetValue<string>().ShouldBe("xml-secret-header");
    }

    [Fact]
    public async Task response_metadata_examples_are_applied_to_response_content()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var example = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/response-metadata-example"]!["post"]!
            ["responses"]!["201"]!["content"]!["application/json"]!["example"]!;

        example["message"]!.GetValue<string>().ShouldBe("from response metadata");
    }

    [Fact]
    public async Task explicit_response_examples_override_response_metadata_examples()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var example = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/explicit-response-example"]!["post"]!
            ["responses"]!["200"]!["content"]!["application/json"]!["example"]!;

        example["message"]!.GetValue<string>().ShouldBe("from explicit response examples");
    }

    [Fact]
    public async Task non_fastendpoint_auth_metadata_uses_configured_security_schemes()
    {
        var json = await App.GetDocumentJsonAsync("Release 1.0");
        var doc = JsonNode.Parse(json)!;
        var securedOperation = doc["paths"]!["/non-fe-auth"]!["get"]!;
        var anonymousOperation = doc["paths"]!["/non-fe-auth-anon"]!["get"]!;
        var securitySchemeNames = securedOperation["security"]!.AsArray()
                                  .SelectMany(o => o!.AsObject().Select(p => p.Key))
                                  .ToArray();

        securitySchemeNames.ShouldContain("JWTBearerAuth");
        securitySchemeNames.ShouldContain("ApiKey");
        (anonymousOperation["security"] is JsonArray sec && sec.Count > 0).ShouldBeFalse();
    }

    [Fact]
    public async Task interface_dictionary_query_parameter_uses_object_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/interface-dictionary"]!["get"]!;
        var dictParam = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "metadata");

        dictParam["schema"].ShouldBeNull();
        dictParam["content"]!["application/json"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("object");
        dictParam["content"]!["application/json"]!["schema"]!["additionalProperties"]!["type"]!.GetValue<string>().ShouldBe("string");
    }

    [Fact]
    public async Task manually_added_complex_parameter_and_header_refs_have_component_schemas()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var nestedRef = "#/components/schemas/TestCasesSwaggerReviewManualSchemaNested";
        var idempotencyRef = "#/components/schemas/TestCasesSwaggerReviewManualSchemaIdempotencyHeader";
        var queryParam = doc["paths"]!["/api/swagger-review/manual-complex-query"]!["get"]!["parameters"]!.AsArray()
            .First(p => p["name"]!.GetValue<string>() == "filter");
        var responseHeader = doc["paths"]!["/api/swagger-review/manual-complex-response-header"]!["get"]!["responses"]!["200"]!
            ["headers"]!["x-complex-header"]!;
        var idempotencyHeader = doc["paths"]!["/api/swagger-review/manual-complex-idempotency-header"]!["post"]!["parameters"]!.AsArray()
            .First(p => p["name"]!.GetValue<string>() == "Idempotency-Key");

        queryParam["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().ShouldBe(nestedRef);
        responseHeader["schema"]!["$ref"]!.GetValue<string>().ShouldBe(nestedRef);
        idempotencyHeader["schema"]!["$ref"]!.GetValue<string>().ShouldBe(idempotencyRef);
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewManualSchemaNested"].ShouldNotBeNull();
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewManualSchemaIdempotencyHeader"].ShouldNotBeNull();
    }

    [Fact]
    public async Task xml_docs_are_applied_for_properties_on_generic_types()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewGenericXmlDocReviewRequest"]!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewGenericXmlDocReviewResponse"]!;

        requestSchema["properties"]!["value"]!["description"]!.GetValue<string>().ShouldBe("wrapped value summary");
        requestSchema["properties"]!["value"]!["example"]!.GetValue<string>().ShouldBe("wrapped example");
        responseSchema["description"]!.GetValue<string>().ShouldBe("generic review response summary");
        responseSchema["properties"]!["value"]!["description"]!.GetValue<string>().ShouldBe("wrapped value summary");
    }

    [Fact]
    public async Task xml_doc_inline_markup_text_is_preserved_in_descriptions()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewInlineMarkupXmlDocReviewRequest"]!;

        requestSchema["description"]!.GetValue<string>().ShouldBe("returns the User record.");
        requestSchema["properties"]!["userId"]!["description"]!.GetValue<string>().ShouldBe("filter by UserId value.");
    }

    [Fact]
    public async Task endpoint_xml_docs_are_applied_to_operation_summary_and_description()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/endpoint-xml-doc"]!["get"]!;

        operation["summary"]!.GetValue<string>().ShouldBe("xml endpoint summary");
        operation["description"]!.GetValue<string>().ShouldBe("xml endpoint remarks");
    }

    [Fact]
    public async Task endpoint_summary_values_override_endpoint_xml_docs()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/endpoint-summary-overrides-xml-doc"]!["get"]!;

        operation["summary"]!.GetValue<string>().ShouldBe("configured endpoint summary");
        operation["description"]!.GetValue<string>().ShouldBe("configured endpoint description");
    }

    [Fact]
    public async Task missing_schema_generation_uses_primitive_formats_for_primitive_like_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewMissingSchemaPrimitiveResponse"]!;

        responseSchema["properties"]!["correlationId"]!["type"]!.GetValue<string>().ShouldBe("string");
        responseSchema["properties"]!["correlationId"]!["format"]!.GetValue<string>().ShouldBe("uuid");
        responseSchema["properties"]!["effectiveOn"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/SystemDateOnly");
        doc["components"]!["schemas"]!["SystemDateOnly"]!["type"]!.GetValue<string>().ShouldBe("string");
        doc["components"]!["schemas"]!["SystemDateOnly"]!["format"]!.GetValue<string>().ShouldBe("date");
    }

    [Fact]
    public async Task missing_schema_generation_runs_schema_transformers()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JsonNode.Parse(json)!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewMissingSchemaEnumResponse"]!;
        var enumSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUlongEnumReviewStatus"]!;

        responseSchema["properties"]!["status"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/TestCasesSwaggerReviewUlongEnumReviewStatus");
        enumSchema["enum"]![0]!.ToString().ShouldBe("Max");
    }

    [Fact]
    public async Task orphan_constrained_route_param_uses_constraint_type()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/test-cases/ep-witout-req-route-binding-test/{customerID}/{otherID}"]!["get"]!;
        var customerId = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "customerID");

        customerId["schema"]!["type"]!.GetValue<string>().ShouldBe("integer");
        customerId["schema"]!["format"]!.GetValue<string>().ShouldBe("int32");
    }

    [Fact]
    public async Task endpoint_without_request_does_not_use_response_metadata_for_request_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/no-request-metadata-leak/{leakId}"]!["get"]!;
        var leakId = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "leakId");

        leakId["description"].ShouldBeNull();
    }

    [Fact]
    public async Task inline_default_route_values_are_removed_from_openapi_paths_and_parameter_names()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/default-route-value/{id}"]!["get"]!;
        var id = operation["parameters"]!.AsArray().First(p => p["name"]!.GetValue<string>() == "id");

        id["description"]!.GetValue<string>().ShouldBe("route param summary");
    }

    [Fact]
    public async Task multi_route_endpoint_uses_path_parameters_from_matching_route_only()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JsonNode.Parse(json)!;
        var saveOperation = doc["paths"]!["/api/customer/save"]!["get"]!;
        var pathParams = saveOperation["parameters"]!.AsArray()
                         .Where(p => p["in"]!.GetValue<string>() == "path")
                         .Select(p => p["name"]!.GetValue<string>())
                         .ToArray();
        var routedOperation = doc["paths"]!["/api/customer/{cID}/new/{sourceID}"]!["get"]!;
        var routedParams = routedOperation["parameters"]!.AsArray()
                           .Select(
                               p => new
                               {
                                   Name = p["name"]!.GetValue<string>(),
                                   Location = p["in"]!.GetValue<string>()
                               })
                           .ToArray();

        pathParams.ShouldBeEmpty();
        routedParams.ShouldContain(p => p.Name == "cID" && p.Location == "path");
        routedParams.ShouldContain(p => p.Name == "sourceID" && p.Location == "path");
        routedParams.ShouldNotContain(p => p.Name == "refererID");
    }

    [Fact]
    public async Task initial_release_does_not_keep_lone_unshared_operation_variants()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JsonNode.Parse(json)!.AsObject();
        var schemas = doc["components"]!["schemas"]!.AsObject();
        var variantIds = schemas.Select(p => p.Key)
                                .Where(static n => n.Contains("__op", StringComparison.Ordinal))
                                .ToArray();
        var offenders = new List<string>();

        foreach (var group in variantIds.GroupBy(GetVariantSourceRefId, StringComparer.Ordinal))
        {
            if (group.Count() != 1)
                continue;

            var sourceRefId = group.Key;
            var sourceExists = schemas.ContainsKey(sourceRefId);
            var sourceRefToken = $"#/components/schemas/{sourceRefId}";
            var sourceRefCount = DescendantObjects(doc)
                                    .Count(o => string.Equals(o["$ref"]?.GetValue<string>(), sourceRefToken, StringComparison.Ordinal));

            if (!sourceExists && sourceRefCount == 0)
                offenders.Add(group.Single());
        }

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public async Task nullable_collection_property_inlines_array_items_and_accepts_null()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JsonNode.Parse(json)!;
        var customersSchema = doc["components"]!["schemas"]!["CustomersListRecentResponse"]!["properties"]!["customers"]!;

        SchemaTypeContains(customersSchema, "null").ShouldBeTrue();
        SchemaTypeContains(customersSchema, "array").ShouldBeTrue();
        customersSchema["items"].ShouldNotBeNull();
        customersSchema["oneOf"].ShouldBeNull();
        customersSchema["anyOf"].ShouldBeNull();
        SchemaAcceptsNull(doc, customersSchema).ShouldBeTrue();
    }

    [Fact]
    public async Task nullable_ref_property_oneOf_preserves_null_and_reference_branches()
    {
        var json = await App.GetDocumentJsonAsync("Nullable OneOf Repro");
        var nullableObjSchema = JsonNode.Parse(json)!["components"]!["schemas"]!["TestCasesSwaggerReviewNullableRefPropertyResponse"]!
            ["properties"]!["nullableObj"]!;
        var oneOf = nullableObjSchema["oneOf"] as JsonArray ?? [];

        oneOf.Count.ShouldBe(2);
        oneOf.Count(s => SchemaTypeContains(s, "null")).ShouldBe(1);
        oneOf.Select(s => s["$ref"]?.GetValue<string>())
             .Where(static r => r is not null)
             .ShouldBe(["#/components/schemas/TestCasesSwaggerReviewNullableRefChild"]);
    }

    [Fact]
    public async Task nullable_ref_property_schema_accepts_null()
    {
        var json = await App.GetDocumentJsonAsync("Nullable OneOf Repro");
        var doc = JsonNode.Parse(json)!;
        var nullableObjSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewNullableRefPropertyResponse"]!["properties"]!["nullableObj"]!;

        SchemaAcceptsNull(doc, nullableObjSchema).ShouldBeTrue();
    }

    [Fact]
    public async Task nullable_typed_schemas_with_composition_must_accept_null()
    {
        var offenders = new List<string>();

        foreach (var documentName in new[] { "Initial Release", "Nullable OneOf Repro" })
        {
            var json = await App.GetDocumentJsonAsync(documentName);
            var doc = JsonNode.Parse(json)!;

            foreach (var schema in DescendantObjects(doc))
            {
                if (!SchemaTypeContains(schema, "null"))
                    continue;

                if (schema["oneOf"] is JsonArray oneOf && oneOf.Count > 0 && !OneOfAcceptsNull(doc, oneOf))
                    offenders.Add($"{documentName}: {schema.GetPath()}.oneOf");

                if (schema["anyOf"] is JsonArray anyOf && anyOf.Count > 0 && !AnyOfAcceptsNull(doc, anyOf))
                    offenders.Add($"{documentName}: {schema.GetPath()}.anyOf");
            }
        }

        offenders.ShouldBeEmpty();
    }

    static JsonNode ResolveSchema(JsonNode document, JsonNode schema)
    {
        var refValue = schema["$ref"]?.GetValue<string>();

        if (refValue is null)
            return schema;

        var schemaKey = refValue[(refValue.LastIndexOf('/') + 1)..];

        return document["components"]!["schemas"]![schemaKey]!;
    }

    static bool SchemaAcceptsNull(JsonNode document, JsonNode schema)
        => SchemaAcceptsNull(document, schema, []);

    static bool SchemaAcceptsNull(JsonNode document, JsonNode schema, HashSet<string> visitedRefs)
    {
        var refValue = schema["$ref"]?.GetValue<string>();

        if (refValue is not null)
        {
            if (!visitedRefs.Add(refValue))
                return false;

            return SchemaAcceptsNull(document, ResolveSchema(document, schema), visitedRefs);
        }

        if (schema["allOf"] is JsonArray allOf && allOf.Count > 0 && allOf.Any(s => !SchemaAcceptsNull(document, s!, new(visitedRefs, StringComparer.Ordinal))))
            return false;

        if (schema["oneOf"] is JsonArray oneOf && oneOf.Count > 0 && !OneOfAcceptsNull(document, oneOf, visitedRefs))
            return false;

        if (schema["anyOf"] is JsonArray anyOf && anyOf.Count > 0 && !AnyOfAcceptsNull(document, anyOf, visitedRefs))
            return false;

        if (schema["not"] is { } not && SchemaAcceptsNull(document, not, new(visitedRefs, StringComparer.Ordinal)))
            return false;

        return schema["type"] is null || SchemaTypeContains(schema, "null");
    }

    static bool OneOfAcceptsNull(JsonNode document, JsonArray oneOf)
        => OneOfAcceptsNull(document, oneOf, []);

    static bool OneOfAcceptsNull(JsonNode document, JsonArray oneOf, HashSet<string> visitedRefs)
        => oneOf.Count(s => SchemaAcceptsNull(document, s!, new(visitedRefs, StringComparer.Ordinal))) == 1;

    static bool AnyOfAcceptsNull(JsonNode document, JsonArray anyOf)
        => AnyOfAcceptsNull(document, anyOf, []);

    static bool AnyOfAcceptsNull(JsonNode document, JsonArray anyOf, HashSet<string> visitedRefs)
        => anyOf.Any(s => SchemaAcceptsNull(document, s!, new(visitedRefs, StringComparer.Ordinal)));

    static bool SchemaTypeContains(JsonNode schema, string type)
        => schema["type"] switch
        {
            JsonArray types => types.StringValues().Contains(type, StringComparer.Ordinal),
            JsonValue value => string.Equals(value.GetValue<string>(), type, StringComparison.Ordinal),
            _ => false
        };

    static IEnumerable<JsonObject> DescendantObjects(JsonNode? token)
    {
        switch (token)
        {
            case JsonObject obj:
                yield return obj;
                foreach (var prop in obj)
                {
                    foreach (var descendant in DescendantObjects(prop.Value))
                        yield return descendant;
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    foreach (var descendant in DescendantObjects(item))
                        yield return descendant;
                }
                break;
        }
    }


    static string GetVariantSourceRefId(string refId)
    {
        var suffixIndex = refId.LastIndexOf("__op", StringComparison.Ordinal);

        return suffixIndex < 0 ? refId : refId[..suffixIndex];
    }

    [Fact]
    public async Task get_request_has_no_request_body_and_uses_query_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var op = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/bodyless-query-params"]!["get"]!;

        op.ShouldNotBeNull();
        op["requestBody"].ShouldBeNull();

        var parameters = op["parameters"]!.AsArray().ToArray();
        parameters.ShouldContain(p => p["name"]!.GetValue<string>() == "name" && p["in"]!.GetValue<string>() == "query");
        parameters.ShouldContain(p => p["name"]!.GetValue<string>() == "page" && p["in"]!.GetValue<string>() == "query");
    }

    [Fact]
    public async Task head_request_has_no_request_body_and_uses_query_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var op = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/bodyless-query-params"]!["head"]!;

        op.ShouldNotBeNull();
        op["requestBody"].ShouldBeNull();

        var parameters = op["parameters"]!.AsArray().ToArray();
        parameters.ShouldContain(p => p["name"]!.GetValue<string>() == "name" && p["in"]!.GetValue<string>() == "query");
        parameters.ShouldContain(p => p["name"]!.GetValue<string>() == "page" && p["in"]!.GetValue<string>() == "query");
    }

    [Theory, InlineData("/api/swagger-review/root-list-body", "get"), InlineData("/api/swagger-review/root-list-body", "head"),
     InlineData("/api/swagger-review/root-array-body", "get")]
    public async Task bodyless_root_collection_request_body_is_optional_and_keeps_array_schema(string path, string method)
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var document = JsonNode.Parse(json)!;
        var requestBody = document["paths"]![path]![method]!["requestBody"]!;

        requestBody.ShouldNotBeNull();
        (requestBody["required"]?.GetValue<bool>() ?? false).ShouldBeFalse();

        var content = requestBody["content"]!;
        var mediaType = content["application/json"] ?? content["*/*"] ?? content.AsObject().First().Value;
        var schema = ResolveSchema(document, mediaType["schema"]!);
        schema["type"]!.GetValue<string>().ShouldBe("array");
        schema["items"].ShouldNotBeNull();
    }

    [Fact]
    public async Task post_root_collection_request_body_remains_required()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var requestBody = JsonNode.Parse(json)!["paths"]!["/api/swagger-review/root-list-body"]!["post"]!["requestBody"]!;

        requestBody.ShouldNotBeNull();
        requestBody["required"]!.GetValue<bool>().ShouldBeTrue();
    }
}


static file class JsonNodeTestExtensions
{
    public static IEnumerable<string> StringValues(this JsonNode? node)
        => node is JsonArray arr
               ? arr.Select(n => n!.GetValue<string>())
               : [];
}
