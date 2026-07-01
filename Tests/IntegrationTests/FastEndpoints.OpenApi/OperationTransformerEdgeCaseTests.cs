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
    public async Task query_method_endpoint_is_omitted_when_openapi_tooling_drops_unknown_method()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);

        doc["paths"]!["/api/swagger-review/query-method"].ShouldBeNull();
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
        var alphaSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-request-metadata-alpha"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var betaSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-request-metadata-beta"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var componentSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedRequestMetadataReviewRequest"];

        alphaSchema["example"]!["name"]!.Value<string>().ShouldBe("alpha example");
        betaSchema["example"]!["name"]!.Value<string>().ShouldBe("beta example");
        alphaSchema["properties"]!["name"]!["description"]!.Value<string>().ShouldBe("alpha description");
        betaSchema["properties"]!["name"]!["description"]!.Value<string>().ShouldBe("beta description");
        componentSchema.ShouldBeNull();
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
        doc["components"]!["schemas"]!["TestCasesSwaggerReviewVersionPrefilterSharedRequest"]!["properties"]!["name"]!["description"]!
            .Value<string>()
            .ShouldBe("initial description");
        ((JObject)doc["components"]!["schemas"]!).Properties().Any(p => p.Name.StartsWith("TestCasesSwaggerReviewVersionPrefilterSharedRequest__op", StringComparison.Ordinal))
                                                 .ShouldBeFalse();
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

        enumValue.ToString().ShouldBe("Max");
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
    public async Task default_value_attributes_are_applied_to_request_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/default-value-schema"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

        requestSchema["properties"]!["name"]!["default"]!.Value<string>().ShouldBe("schema-default");
        requestSchema["properties"]!["count"]!["default"]!.Value<int>().ShouldBe(7);
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

        var requiredSearchType = requiredSearch["schema"]!["type"]!;
        requiredSearchType.Type.ShouldBe(JTokenType.Array);
        requiredSearchType.Values<string>().ShouldContain("string");
        requiredSearchType.Values<string>().ShouldContain("null");

        var optionalFilterType = optionalFilter["schema"]!["type"]!;
        optionalFilterType.Type.ShouldBe(JTokenType.Array);
        optionalFilterType.Values<string>().ShouldContain("string");
        optionalFilterType.Values<string>().ShouldContain("null");
    }

    [Fact]
    public async Task unique_items_are_emitted_for_scalar_set_types_and_explicit_opt_in()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUniqueItemsReviewRequest"]!;
        var responseSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewUniqueItemsReviewResponse"]!;
        var hashSetStringSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericHashSetOfSystemString"]!;
        var hashSetChildSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericHashSetOfTestCasesSwaggerReviewUniqueItemsReviewChild"]!;
        var listChildSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild"]!;
        var sortedSetIntSchema = doc["components"]!["schemas"]!["SystemCollectionsGenericSortedSetOfSystemInt32"]!;

        requestSchema["properties"]!["autoTags"]!["$ref"]!.Value<string>().ShouldBe("#/components/schemas/SystemCollectionsGenericHashSetOfSystemString");
        requestSchema["properties"]!["autoChildren"]!["$ref"]!.Value<string>()
                                                              .ShouldBe("#/components/schemas/SystemCollectionsGenericHashSetOfTestCasesSwaggerReviewUniqueItemsReviewChild");
        requestSchema["properties"]!["explicitChildren"]!["$ref"]!.Value<string>()
                                                                  .ShouldBe("#/components/schemas/SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild");

        hashSetStringSchema["uniqueItems"]!.Value<bool>().ShouldBeTrue();
        hashSetChildSchema["uniqueItems"].ShouldBeNull();
        listChildSchema["uniqueItems"]!.Value<bool>().ShouldBeTrue();
        sortedSetIntSchema["uniqueItems"]!.Value<bool>().ShouldBeTrue();

        responseSchema["properties"]!["autoIds"]!["$ref"]!.Value<string>().ShouldBe("#/components/schemas/SystemCollectionsGenericSortedSetOfSystemInt32");
        responseSchema["properties"]!["explicitChildren"]!["$ref"]!.Value<string>()
                                                                   .ShouldBe("#/components/schemas/SystemCollectionsGenericListOfTestCasesSwaggerReviewUniqueItemsReviewChild");
    }

    [Fact]
    public async Task promoted_body_schema_keeps_validation_rules_from_promoted_property_subtree()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/promoted-body-validation/{id}"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = ResolveSchema(doc, requestSchema["properties"]!["child"]!);

        requestSchema["properties"]!["body"].ShouldBeNull();
        requestSchema["properties"]!["name"]!["minLength"]!.Value<int>().ShouldBe(3);
        requestSchema["required"]!.Values<string>().ShouldContain("name");
        requestSchema["properties"]!["child"]!["$ref"]!.Value<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewPromotedBodyValidationChild");
        childSchema["properties"]!["code"]!["minLength"]!.Value<int>().ShouldBe(2);
        childSchema["required"]!.Values<string>().ShouldContain("code");
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
    public async Task hide_from_docs_properties_are_removed_from_request_and_response_schemas()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
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
        var operation = JToken.Parse(json)["paths"]!["/api/test-cases/from-body-binding/{id}"]!["post"]!;

        var schema = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;

        schema = ResolveSchema(JToken.Parse(json), schema);
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
        schema = ResolveSchema(JToken.Parse(json), schema);
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
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/child-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = ResolveSchema(doc, requestSchema["properties"]!["child"]!);

        requestSchema["properties"]!["child"]!["$ref"]!.Value<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewChildValidatorReviewChild");
        childSchema["properties"]!["score"]!["exclusiveMinimum"]!.Value<int>().ShouldBe(10);
    }

    [Fact]
    public async Task deep_nested_validator_rules_are_applied_to_operation_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/deep-nested-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var childSchema = doc["components"]!["schemas"]!["TestCasesSwaggerReviewDeepNestedValidatorReviewChild"]!;
        var grandChildSchema = ResolveSchema(doc, childSchema["properties"]!["subChild"]!);

        ResolveSchema(doc, requestSchema["properties"]!["child"]!).ShouldBe(childSchema);
        childSchema["properties"]!["subChild"]!["$ref"]!.Value<string>()
                                                        .ShouldBe("#/components/schemas/TestCasesSwaggerReviewDeepNestedValidatorReviewGrandChild");
        grandChildSchema["properties"]!["field"]!["minLength"]!.Value<int>().ShouldBe(5);
    }

    [Fact]
    public async Task parent_path_validator_rules_do_not_mutate_shared_nested_component_schema()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var alphaRequest = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/shared-nested-validation-alpha"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var betaRequest = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationBetaRequest"]!;
        var addressComponent = doc["components"]!["schemas"]!["TestCasesSwaggerReviewSharedNestedValidationAddress"]!;

        var alphaAddress = ResolveSchema(doc, alphaRequest["properties"]!["address"]!);
        alphaAddress["required"]!.Values<string>().ShouldContain("zip");
        betaRequest["properties"]!["address"]!["$ref"]!.Value<string>()
                                                       .ShouldBe("#/components/schemas/TestCasesSwaggerReviewSharedNestedValidationAddress");
        addressComponent["required"].ShouldBeNull();
    }

    [Fact]
    public async Task validator_rules_are_applied_through_intermediate_non_generic_base_type()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var doc = JToken.Parse(json);
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/swagger-review/intermediate-base-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

        requestSchema["properties"]!["name"]!["minLength"]!.Value<int>().ShouldBe(3);
    }

    [Fact]
    public async Task included_validator_rules_are_applied_to_schema_properties()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JToken.Parse(json);
        var requestSchema = ResolveSchema(
            doc,
            doc["paths"]!["/api/test-cases/included-validator"]!["post"]!
                ["requestBody"]!["content"]!["application/json"]!["schema"]!);

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
    public async Task to_header_response_properties_use_xml_docs_for_description_and_example()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var header = JToken.Parse(json)["paths"]!["/api/swagger-review/json-property-name-transformers"]!["post"]!
            ["responses"]!["200"]!["headers"]!["x-secret"]!;

        header["description"]!.Value<string>().ShouldBe("secret header summary");
        header["example"]!.Value<string>().ShouldBe("xml-secret-header");
    }

    [Fact]
    public async Task response_metadata_examples_are_applied_to_response_content()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var example = JToken.Parse(json)["paths"]!["/api/swagger-review/response-metadata-example"]!["post"]!
            ["responses"]!["201"]!["content"]!["application/json"]!["example"]!;

        example["message"]!.Value<string>().ShouldBe("from response metadata");
    }

    [Fact]
    public async Task explicit_response_examples_override_response_metadata_examples()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var example = JToken.Parse(json)["paths"]!["/api/swagger-review/explicit-response-example"]!["post"]!
            ["responses"]!["200"]!["content"]!["application/json"]!["example"]!;

        example["message"]!.Value<string>().ShouldBe("from explicit response examples");
    }

    [Fact]
    public async Task non_fastendpoint_auth_metadata_uses_configured_security_schemes()
    {
        var json = await App.GetDocumentJsonAsync("Release 1.0");
        var doc = JToken.Parse(json);
        var securedOperation = doc["paths"]!["/non-fe-auth"]!["get"]!;
        var anonymousOperation = doc["paths"]!["/non-fe-auth-anon"]!["get"]!;
        var securitySchemeNames = securedOperation["security"]!
                                  .Children<JObject>()
                                  .SelectMany(o => o.Properties().Select(p => p.Name))
                                  .ToArray();

        securitySchemeNames.ShouldContain("JWTBearerAuth");
        securitySchemeNames.ShouldContain("ApiKey");
        (anonymousOperation["security"]?.Any() == true).ShouldBeFalse();
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
    public async Task endpoint_xml_docs_are_applied_to_operation_summary_and_description()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/endpoint-xml-doc"]!["get"]!;

        operation["summary"]!.Value<string>().ShouldBe("xml endpoint summary");
        operation["description"]!.Value<string>().ShouldBe("xml endpoint remarks");
    }

    [Fact]
    public async Task endpoint_summary_values_override_endpoint_xml_docs()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var operation = JToken.Parse(json)["paths"]!["/api/swagger-review/endpoint-summary-overrides-xml-doc"]!["get"]!;

        operation["summary"]!.Value<string>().ShouldBe("configured endpoint summary");
        operation["description"]!.Value<string>().ShouldBe("configured endpoint description");
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
        enumSchema["enum"]![0]!.ToString().ShouldBe("Max");
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
                           .Select(
                               p => new
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

    [Fact]
    public async Task initial_release_does_not_keep_lone_unshared_operation_variants()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = (JObject)JToken.Parse(json);
        var schemas = (JObject)doc["components"]!["schemas"]!;
        var variantIds = schemas.Properties()
                                .Select(p => p.Name)
                                .Where(static n => n.Contains("__op", StringComparison.Ordinal))
                                .ToArray();
        var offenders = new List<string>();

        foreach (var group in variantIds.GroupBy(GetVariantSourceRefId, StringComparer.Ordinal))
        {
            if (group.Count() != 1)
                continue;

            var sourceRefId = group.Key;
            var sourceExists = schemas.Property(sourceRefId) is not null;
            var sourceRefToken = $"#/components/schemas/{sourceRefId}";
            var sourceRefCount = doc.SelectTokens("$..$ref")
                                    .Count(t => string.Equals(t.Value<string>(), sourceRefToken, StringComparison.Ordinal));

            if (!sourceExists && sourceRefCount == 0)
                offenders.Add(group.Single());
        }

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public async Task nullable_collection_property_inlines_array_items_and_accepts_null()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var doc = JToken.Parse(json);
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
        var nullableObjSchema = JToken.Parse(json)["components"]!["schemas"]!["TestCasesSwaggerReviewNullableRefPropertyResponse"]!
            ["properties"]!["nullableObj"]!;
        var oneOf = nullableObjSchema["oneOf"] as JArray ?? [];

        oneOf.Count.ShouldBe(2);
        oneOf.Count(s => SchemaTypeContains(s, "null")).ShouldBe(1);
        oneOf.Select(s => s["$ref"]?.Value<string>())
             .Where(static r => r is not null)
             .ShouldBe(["#/components/schemas/TestCasesSwaggerReviewNullableRefChild"]);
    }

    [Fact]
    public async Task nullable_ref_property_schema_accepts_null()
    {
        var json = await App.GetDocumentJsonAsync("Nullable OneOf Repro");
        var doc = JToken.Parse(json);
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
            var doc = JToken.Parse(json);

            foreach (var schema in DescendantObjects(doc))
            {
                if (!SchemaTypeContains(schema, "null"))
                    continue;

                if (schema["oneOf"] is JArray oneOf && oneOf.Count > 0 && !OneOfAcceptsNull(doc, oneOf))
                    offenders.Add($"{documentName}: {schema.Path}.oneOf");

                if (schema["anyOf"] is JArray anyOf && anyOf.Count > 0 && !AnyOfAcceptsNull(doc, anyOf))
                    offenders.Add($"{documentName}: {schema.Path}.anyOf");
            }
        }

        offenders.ShouldBeEmpty();
    }

    static JToken ResolveSchema(JToken document, JToken schema)
    {
        var refValue = schema["$ref"]?.Value<string>();

        if (refValue is null)
            return schema;

        var schemaKey = refValue[(refValue.LastIndexOf('/') + 1)..];

        return document["components"]!["schemas"]![schemaKey]!;
    }

    static bool SchemaAcceptsNull(JToken document, JToken schema)
        => SchemaAcceptsNull(document, schema, []);

    static bool SchemaAcceptsNull(JToken document, JToken schema, HashSet<string> visitedRefs)
    {
        var refValue = schema["$ref"]?.Value<string>();

        if (refValue is not null)
        {
            if (!visitedRefs.Add(refValue))
                return false;

            return SchemaAcceptsNull(document, ResolveSchema(document, schema), visitedRefs);
        }

        if (schema["allOf"] is JArray allOf && allOf.Count > 0 && allOf.Any(s => !SchemaAcceptsNull(document, s, new(visitedRefs, StringComparer.Ordinal))))
            return false;

        if (schema["oneOf"] is JArray oneOf && oneOf.Count > 0 && !OneOfAcceptsNull(document, oneOf, visitedRefs))
            return false;

        if (schema["anyOf"] is JArray anyOf && anyOf.Count > 0 && !AnyOfAcceptsNull(document, anyOf, visitedRefs))
            return false;

        if (schema["not"] is { } not && SchemaAcceptsNull(document, not, new(visitedRefs, StringComparer.Ordinal)))
            return false;

        return schema["type"] is null || SchemaTypeContains(schema, "null");
    }

    static bool OneOfAcceptsNull(JToken document, JArray oneOf)
        => OneOfAcceptsNull(document, oneOf, []);

    static bool OneOfAcceptsNull(JToken document, JArray oneOf, HashSet<string> visitedRefs)
        => oneOf.Count(s => SchemaAcceptsNull(document, s, new(visitedRefs, StringComparer.Ordinal))) == 1;

    static bool AnyOfAcceptsNull(JToken document, JArray anyOf)
        => AnyOfAcceptsNull(document, anyOf, []);

    static bool AnyOfAcceptsNull(JToken document, JArray anyOf, HashSet<string> visitedRefs)
        => anyOf.Any(s => SchemaAcceptsNull(document, s, new(visitedRefs, StringComparer.Ordinal)));

    static bool SchemaTypeContains(JToken schema, string type)
        => schema["type"] switch
        {
            JArray types => types.Values<string>().Contains(type, StringComparer.Ordinal),
            JValue value => string.Equals(value.Value<string>(), type, StringComparison.Ordinal),
            _ => false
        };

    static IEnumerable<JObject> DescendantObjects(JToken token)
    {
        if (token is JObject obj)
            yield return obj;

        foreach (var child in token.Children())
        {
            foreach (var descendant in DescendantObjects(child))
                yield return descendant;
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
        var op = JToken.Parse(json)["paths"]!["/api/swagger-review/bodyless-query-params"]!["get"]!;

        op.ShouldNotBeNull();
        op["requestBody"].ShouldBeNull();

        var parameters = op["parameters"]!.ToArray();
        parameters.ShouldContain(p => p["name"]!.Value<string>() == "name" && p["in"]!.Value<string>() == "query");
        parameters.ShouldContain(p => p["name"]!.Value<string>() == "page" && p["in"]!.Value<string>() == "query");
    }

    [Fact]
    public async Task head_request_has_no_request_body_and_uses_query_parameters()
    {
        var json = await App.GetDocumentJsonAsync("Swagger Review");
        var op = JToken.Parse(json)["paths"]!["/api/swagger-review/bodyless-query-params"]!["head"]!;

        op.ShouldNotBeNull();
        op["requestBody"].ShouldBeNull();

        var parameters = op["parameters"]!.ToArray();
        parameters.ShouldContain(p => p["name"]!.Value<string>() == "name" && p["in"]!.Value<string>() == "query");
        parameters.ShouldContain(p => p["name"]!.Value<string>() == "page" && p["in"]!.Value<string>() == "query");
    }

    [Theory, InlineData("/api/swagger-review/root-list-body", "get"), InlineData("/api/swagger-review/root-list-body", "head"),
     InlineData("/api/swagger-review/root-array-body", "get")]
    public async Task bodyless_root_collection_request_body_is_optional_and_keeps_array_schema(string path, string method)
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var document = JToken.Parse(json);
        var requestBody = document["paths"]![path]![method]!["requestBody"]!;

        requestBody.ShouldNotBeNull();
        (requestBody["required"]?.Value<bool>() ?? false).ShouldBeFalse();

        var content = requestBody["content"]!;
        var mediaType = content["application/json"] ?? content["*/*"] ?? content.Children<JProperty>().First().Value;
        var schema = ResolveSchema(document, mediaType["schema"]!);
        schema["type"]!.Value<string>().ShouldBe("array");
        schema["items"].ShouldNotBeNull();
    }

    [Fact]
    public async Task post_root_collection_request_body_remains_required()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");
        var requestBody = JToken.Parse(json)["paths"]!["/api/swagger-review/root-list-body"]!["post"]!["requestBody"]!;

        requestBody.ShouldNotBeNull();
        requestBody["required"]!.Value<bool>().ShouldBeTrue();
    }
}