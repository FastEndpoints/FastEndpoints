using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.OpenApi;

namespace OpenApi;

public class OperationSchemaHelpersTests
{
    [Fact]
    public void clone_as_concrete_schema_deep_copies_mutable_members()
    {
        IOpenApiSchema schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "name" },
            Example = new JsonObject { ["name"] = "original" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["nested"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            }
        };

        var original = (OpenApiSchema)schema;
        var clone = schema.CloneAsConcreteSchema()!;

        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(schema);
        clone.Required!.ShouldNotBeSameAs(original.Required);
        clone.Properties!.ShouldNotBeSameAs(original.Properties);
        clone.Example.ShouldNotBeSameAs(original.Example);

        clone.Required!.Add("other");
        ((JsonObject)clone.Example!)["name"] = "clone";
        ((OpenApiSchema)clone.Properties!["name"]).Properties!.Remove("nested");

        original.Required.ShouldBe(["name"]);
        original.Properties!.Keys.ShouldBe(["name"]);
        ((OpenApiSchema)original.Properties!["name"]).Properties!.Keys.ShouldBe(["nested"]);
        original.Example!["name"]!.GetValue<string>().ShouldBe("original");
    }

    [Fact]
    public void clone_logic_accounts_for_all_mutable_openapi_schema_members()
    {
        var unsupportedMutableMembers = typeof(OpenApiSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                             .Where(p => p.CanRead && p.CanWrite)
                                                             .Where(p => !IsKnownCloneableOrShareable(p.PropertyType))
                                                             .Select(p => $"{p.Name}: {p.PropertyType.FullName}")
                                                             .ToArray();

        unsupportedMutableMembers.ShouldBeEmpty();

        static bool IsKnownCloneableOrShareable(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsValueType || type == typeof(string) || type == typeof(Type) || type == typeof(Uri))
                return true;

            if (typeof(JsonNode).IsAssignableFrom(type) ||
                typeof(IOpenApiSchema).IsAssignableFrom(type) ||
                type == typeof(OpenApiSchema) ||
                type == typeof(OpenApiSchemaReference) ||
                type == typeof(IDictionary<string, IOpenApiSchema>) ||
                type == typeof(IDictionary<string, string>) ||
                type == typeof(IDictionary<string, bool>) ||
                type == typeof(IDictionary<string, JsonNode>) ||
                type == typeof(IDictionary<string, HashSet<string>>) ||
                type == typeof(IDictionary<string, IOpenApiExtension>) ||
                type == typeof(IDictionary<string, object>) ||
                type == typeof(IList<IOpenApiSchema>) ||
                type == typeof(ISet<string>) ||
                type == typeof(IList<JsonNode>) ||
                type == typeof(OpenApiDiscriminator) ||
                type == typeof(OpenApiExternalDocs) ||
                type == typeof(OpenApiXml))
                return true;

            return false;
        }
    }

    [Fact]
    public void json_helpers_return_null_when_serialization_fails()
    {
        var value = new ThrowingSerializableObject();

        value.JsonNodeFromObject().ShouldBeNull();
        value.JsonObjectFromObject().ShouldBeNull();
    }

    [Fact]
    public void route_parameter_policy_conversion_uses_raw_route_name_once()
    {
        var opts = new DocumentOptions { UsePropertyNamingPolicy = true };
        var policy = new PrefixNamingPolicy();

        "RouteId".GetOpenApiRouteParameterName(opts, policy).ShouldBe("x_RouteId");
    }

    [Fact]
    public void response_examples_are_cloned_for_each_media_type()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new(),
                        ["application/problem+json"] = new()
                    }
                }
            }
        };
        var epDef = new EndpointDefinition(typeof(object), typeof(object), typeof(object));
        epDef.Summary(s => s.ResponseExamples[200] = new { name = "ok" });
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+ResponseOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext()],
            culture: null)!;

        transformerType.GetMethod("ApplyExamples", BindingFlags.Instance | BindingFlags.Public)!
                       .Invoke(transformer, [operation, epDef]);

        var jsonExample = operation.Responses["200"].Content!["application/json"].Example;
        var problemExample = operation.Responses["200"].Content!["application/problem+json"].Example;

        jsonExample.ShouldNotBeNull();
        problemExample.ShouldNotBeNull();
        problemExample.ShouldNotBeSameAs(jsonExample);
        problemExample.ToJsonString().ShouldBe(jsonExample.ToJsonString());
    }

    [Fact]
    public void promoted_request_wrapper_schemas_are_removed_only_when_unreferenced()
    {
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/promoted"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new()
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new() { Schema = new OpenApiSchemaReference("Body") }
                                }
                            }
                        }
                    }
                },
                ["/still-used"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new()
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new() { Schema = new OpenApiSchemaReference("StillUsedWrapper") }
                                }
                            }
                        }
                    }
                }
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Body"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["PromotedWrapper"] = new OpenApiSchema { Type = JsonSchemaType.Object },
                    ["StillUsedWrapper"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var sharedCtx = new SharedContext();
        sharedCtx.PromotedRequestWrapperSchemaRefs.TryAdd("PromotedWrapper", 0);
        sharedCtx.PromotedRequestWrapperSchemaRefs.TryAdd("StillUsedWrapper", 0);

        document.RemovePromotedRequestWrapperSchemas(sharedCtx);

        document.Components.Schemas.ContainsKey("PromotedWrapper").ShouldBeFalse();
        document.Components.Schemas.ContainsKey("StillUsedWrapper").ShouldBeTrue();
    }

    [Fact]
    public void request_body_property_removal_does_not_apply_naming_policy_when_disabled()
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string> { "QueryValue", "queryValue" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["QueryValue"] = new OpenApiSchema { Type = JsonSchemaType.String },
                                ["queryValue"] = new OpenApiSchema { Type = JsonSchemaType.String }
                            }
                        }
                    }
                }
            }
        };
        var prop = typeof(NamingPolicyRequest).GetProperty(nameof(NamingPolicyRequest.QueryValue))!;
        var removedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        operation.RemovePropFromRequestBody(prop,
                                            new SharedContext(),
                                            new() { UsePropertyNamingPolicy = false },
                                            JsonNamingPolicy.CamelCase,
                                            removedProps);
        var schema = operation.RequestBody!.Content!["application/json"].Schema.ShouldBeOfType<OpenApiSchema>();

        schema.Properties.ShouldNotBeNull();
        schema.Properties.ContainsKey("QueryValue").ShouldBeFalse();
        schema.Properties.ContainsKey("queryValue").ShouldBeTrue();
        schema.Required.ShouldNotBeNull();
        schema.Required.ShouldNotContain("QueryValue");
        schema.Required.ShouldContain("queryValue");
        removedProps.ShouldContain("QueryValue");
    }

    [Fact]
    public void promoted_request_body_schema_is_operation_localized_before_mutation()
    {
        var promotedComponent = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "name" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["Body"] = promotedComponent
                            }
                        }
                    }
                }
            }
        };

        ApplyBodyOverrides(operation, typeof(PromotedBodyRequest));

        var promotedSchema = operation.RequestBody!.Content!["application/json"].Schema.ShouldBeOfType<OpenApiSchema>();

        promotedSchema.ShouldNotBeSameAs(promotedComponent);
        promotedSchema.Properties.ShouldNotBeSameAs(promotedComponent.Properties);
        promotedSchema.Required.ShouldNotBeSameAs(promotedComponent.Required);

        promotedSchema.Description = "endpoint-specific";
        promotedSchema.Properties!.Remove("name");
        promotedSchema.Required!.Remove("name");

        promotedComponent.Description.ShouldBeNull();
        promotedComponent.Properties!.Keys.ShouldBe(["name"]);
        promotedComponent.Required.ShouldBe(["name"]);
    }

    [Fact]
    public void primitive_numeric_parameters_use_numeric_schemas()
    {
        var operation = new OpenApiOperation();
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext()],
            culture: null)!;
        var addParameter = transformerType.GetMethod("AddParameter", BindingFlags.Instance | BindingFlags.NonPublic)!;

        addParameter.Invoke(transformer, [operation, "routeValue", ParameterLocation.Path, null, true, false, typeof(uint)]);
        addParameter.Invoke(transformer, [operation, "queryValue", ParameterLocation.Query, null, null, false, typeof(ulong)]);
        addParameter.Invoke(transformer, [operation, "x-byte-value", ParameterLocation.Header, null, null, false, typeof(byte)]);

        operation.Parameters.ShouldNotBeNull();
        var route = operation.Parameters.Single(p => p.Name == "routeValue");
        var query = operation.Parameters.Single(p => p.Name == "queryValue");
        var header = operation.Parameters.Single(p => p.Name == "x-byte-value");

        route.Schema.ShouldBeOfType<OpenApiSchema>().Type.ShouldBe(JsonSchemaType.Integer);
        ((OpenApiSchema)route.Schema!).Format.ShouldBe("int32");
        query.Schema.ShouldBeOfType<OpenApiSchema>().Type.ShouldBe(JsonSchemaType.Integer);
        ((OpenApiSchema)query.Schema!).Format.ShouldBe("int64");
        header.Schema.ShouldBeOfType<OpenApiSchema>().Type.ShouldBe(JsonSchemaType.Integer);
        ((OpenApiSchema)header.Schema!).Format.ShouldBe("int32");
    }

    [Fact]
    public void type_sample_generation_uses_first_enum_value_and_dictionary_shape()
    {
        var sample = typeof(SampleGenerationRequest).GenerateSampleJsonNode().ShouldBeOfType<JsonObject>();

        sample["Status"]!.GetValue<int>().ShouldBe(5);
        var metadata = sample["Metadata"].ShouldBeOfType<JsonObject>();
        metadata["additionalProp1"]!.GetValue<string>().ShouldBe("additionalProp1");
    }

    [Fact]
    public void schema_sample_generation_prefers_enum_values_and_additional_properties()
    {
        var enumSample = CreateSchemaSample(new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [JsonValue.Create("active")!]
        });
        var dictionarySample = CreateSchemaSample(new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Integer }
        }).ShouldBeOfType<JsonObject>();

        enumSample!.GetValue<string>().ShouldBe("active");
        dictionarySample["additionalProp1"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public void schema_example_normalization_replaces_invalid_enum_values()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [JsonValue.Create("active")!]
        };
        var normalized = NormalizeSchemaExample(JsonValue.Create("invalid")!, schema);

        normalized!.GetValue<string>().ShouldBe("active");
    }

    [Fact]
    public void empty_request_dto_validation_rejects_dtos_without_bindable_properties()
    {
        var ex = Should.Throw<NotSupportedException>(() => ValidateRequestDto(typeof(EmptyOpenApiRequest), false));

        ex.Message.ShouldContain(typeof(EmptyOpenApiRequest).FullName!);
        ex.Message.ShouldContain(typeof(object).FullName!);
    }

    [Fact]
    public void empty_request_dto_validation_allows_collections_empty_request_and_hidden_doc_properties()
    {
        Should.NotThrow(() => ValidateRequestDto(typeof(List<EmptyOpenApiRequest>), true));
        Should.NotThrow(() => ValidateRequestDto(Types.EmptyRequest, false));
        Should.NotThrow(() => ValidateRequestDto(typeof(HiddenDocsBindableRequest), false));
    }

    [Fact]
    public void query_parameter_discovery_respects_dont_bind_query_source()
    {
        ShouldAddQueryParam(nameof(QueryBindingSourcesRequest.DisabledQuery), true).ShouldBeFalse();
        ShouldAddQueryParam(nameof(QueryBindingSourcesRequest.FormOnly), true).ShouldBeFalse();
        ShouldAddQueryParam(nameof(QueryBindingSourcesRequest.RouteOnly), true).ShouldBeFalse();
        ShouldAddQueryParam(nameof(QueryBindingSourcesRequest.ExplicitQuery), false).ShouldBeTrue();
    }

    [Fact]
    public void from_form_promotion_keeps_only_actual_form_content_type()
    {
        var operation = CreateFormPromotionOperation();
        var epDef = new EndpointDefinition(typeof(object), typeof(FromFormPromotionRequest), typeof(object));
        epDef.AllowFormData(urlEncoded: true);

        ApplyBodyOverrides(operation, epDef);

        var content = operation.RequestBody!.Content!;

        content.Keys.ShouldBe(["application/x-www-form-urlencoded"]);
        content["application/x-www-form-urlencoded"].Schema.ShouldBeOfType<OpenApiSchema>()
                                               .Properties!.Keys.ShouldBe(["name"]);
    }

    [Fact]
    public void from_body_promotion_does_not_prune_json_content_types()
    {
        var operation = CreateBodyPromotionOperation();
        var epDef = new EndpointDefinition(typeof(object), typeof(FromBodyPromotionRequest), typeof(object));

        ApplyBodyOverrides(operation, epDef);

        operation.RequestBody!.Content!.Keys.ShouldBe(["application/json", "*/*"]);
    }

    [Fact]
    public void complex_from_query_property_is_documented_as_flattened_query_parameters()
    {
        var operation = new OpenApiOperation();
        var prop = typeof(ComplexFromQueryRequest).GetProperty(nameof(ComplexFromQueryRequest.Filter))!;

        AddComplexFromQueryParameters(operation, prop).ShouldBeTrue();

        var parameters = operation.Parameters!.Cast<OpenApiParameter>().ToArray();

        parameters.Select(p => p.Name).ShouldBe(["City", "zip", "Tags", "Metadata", "Addresses[0].Street"]);
        parameters.Any(p => p.Name == "Filter").ShouldBeFalse();
        parameters.Single(p => p.Name == "Metadata").Content.ShouldNotBeNull();
        parameters.Single(p => p.Name == "City").Content.ShouldBeNull();
    }

    [Fact]
    public void missing_response_content_for_collection_uses_array_schema()
    {
        var response = new OpenApiResponse();
        var sharedCtx = new SharedContext();
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+ResponseOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), sharedCtx],
            culture: null)!;
        var addMissingResponseContent = transformerType.GetMethod("AddMissingResponseContent", BindingFlags.Instance | BindingFlags.NonPublic)!;

        addMissingResponseContent.Invoke(transformer, [response, new ProducesMetadata(typeof(List<MissingSchemaCollectionItem>))]);

        var schema = response.Content!["application/json"].Schema.ShouldBeOfType<OpenApiSchema>();

        schema.Type.ShouldBe(JsonSchemaType.Array);
        schema.Items.ShouldBeOfType<OpenApiSchemaReference>();
        sharedCtx.MissingSchemaTypes.ContainsKey(SchemaNameGenerator.GetReferenceId(typeof(MissingSchemaCollectionItem), false)!).ShouldBeTrue();
        sharedCtx.MissingSchemaTypes.Keys.ShouldNotContain(SchemaNameGenerator.GetReferenceId(typeof(List<MissingSchemaCollectionItem>), false)!);
    }

    sealed class ThrowingSerializableObject
    {
        public string Value => throw new InvalidOperationException("serialization failed");
    }

    sealed class MissingSchemaCollectionItem
    {
        public string Name { get; set; } = string.Empty;
    }

    sealed class NamingPolicyRequest
    {
        public string QueryValue { get; set; } = string.Empty;
    }

    sealed class SampleGenerationRequest
    {
        public SampleGenerationStatus Status { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = [];
    }

    sealed class EmptyOpenApiRequest;

    sealed class HiddenDocsBindableRequest
    {
        [HideFromDocs]
        public string Name { get; set; } = string.Empty;
    }

    sealed class QueryBindingSourcesRequest
    {
        [DontBind(Source.QueryParam)]
        public string DisabledQuery { get; set; } = string.Empty;

        [FormField]
        public string FormOnly { get; set; } = string.Empty;

        [RouteParam]
        public string RouteOnly { get; set; } = string.Empty;

        [QueryParam]
        public string ExplicitQuery { get; set; } = string.Empty;
    }

    sealed class FromFormPromotionRequest
    {
        [FromForm]
        public FormPromotionPayload Payload { get; set; } = new();
    }

    sealed class FromBodyPromotionRequest
    {
        [FromBody]
        public FormPromotionPayload Payload { get; set; } = new();
    }

    sealed class ComplexFromQueryRequest
    {
        [FromQuery]
        public ComplexQueryFilter Filter { get; set; } = new();
    }

    sealed class ComplexQueryFilter
    {
        public string City { get; set; } = string.Empty;

        [BindFrom("zip")]
        public int ZipCode { get; set; }

        public string[] Tags { get; set; } = [];

        public Dictionary<string, string> Metadata { get; set; } = [];

        public List<ComplexQueryAddress> Addresses { get; set; } = [];
    }

    sealed class ComplexQueryAddress
    {
        public string Street { get; set; } = string.Empty;
    }

    sealed class FormPromotionPayload
    {
        public string Name { get; set; } = string.Empty;
    }

    enum SampleGenerationStatus
    {
        Active = 5
    }

    sealed class PromotedBodyRequest
    {
        [FromBody]
        public PromotedBody Body { get; set; } = new();
    }

    sealed class PromotedBody
    {
        public string Name { get; set; } = string.Empty;
    }

    static void ApplyBodyOverrides(OpenApiOperation operation, Type requestType)
        => ApplyBodyOverrides(operation, new EndpointDefinition(typeof(object), requestType, typeof(object)));

    static void ApplyBodyOverrides(OpenApiOperation operation, EndpointDefinition epDef)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext()],
            culture: null)!;

        transformerType.GetMethod("ApplyBodyOverrides", BindingFlags.Instance | BindingFlags.Public)!
                       .Invoke(transformer, [operation, epDef]);
    }

    static OpenApiOperation CreateFormPromotionOperation()
    {
        var operation = CreatePromotionOperation();
        operation.RequestBody!.Content!["application/x-www-form-urlencoded"] = new()
        {
            Schema = ClonePromotionWrapperSchema()
        };

        return operation;
    }

    static OpenApiOperation CreateBodyPromotionOperation()
        => CreatePromotionOperation();

    static OpenApiOperation CreatePromotionOperation()
        => new()
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = ClonePromotionWrapperSchema() },
                    ["*/*"] = new() { Schema = ClonePromotionWrapperSchema() }
                }
            }
        };

    static OpenApiSchema ClonePromotionWrapperSchema()
        => new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Payload"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            }
        };

    static JsonNode? CreateSchemaSample(OpenApiSchema schema)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;

        return (JsonNode?)transformerType.GetMethod("CreateSampleFromSchema", BindingFlags.Static | BindingFlags.NonPublic)!
                                         .Invoke(null, [schema, null]);
    }

    static JsonNode? NormalizeSchemaExample(JsonNode example, OpenApiSchema schema)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;

        return (JsonNode?)transformerType.GetMethod("NormalizeExampleNode", BindingFlags.Static | BindingFlags.NonPublic)!
                                         .Invoke(null, [example, schema, null]);
    }

    static void ValidateRequestDto(Type requestType, bool isCollection)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var epDef = new EndpointDefinition(typeof(object), requestType, typeof(object));

        try
        {
            transformerType.GetMethod("ValidateRequestDto", BindingFlags.Static | BindingFlags.NonPublic)!
                           .Invoke(null, [epDef, requestType, isCollection]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    static bool ShouldAddQueryParam(string propertyName, bool isGetRequest)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var prop = typeof(QueryBindingSourcesRequest).GetProperty(propertyName)!;

        return (bool)transformerType.GetMethod("ShouldAddQueryParam", BindingFlags.Static | BindingFlags.NonPublic)!
                                    .Invoke(null, [prop, new OpenApiOperation(), propertyName, isGetRequest])!;
    }

    static bool AddComplexFromQueryParameters(OpenApiOperation operation, PropertyInfo prop)
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext()],
            culture: null)!;

        return (bool)transformerType.GetMethod("TryAddComplexFromQueryParameters", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .Invoke(transformer, [operation, prop, false])!;
    }

    sealed class ProducesMetadata(Type type) : IProducesResponseTypeMetadata
    {
        public Type? Type { get; } = type;
        public int StatusCode => 200;
        public IEnumerable<string> ContentTypes { get; } = ["application/json"];
    }

    sealed class PrefixNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
            => "x_" + name;
    }
}
