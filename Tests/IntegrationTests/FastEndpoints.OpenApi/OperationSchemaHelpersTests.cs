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
            args: [new DocumentOptions(), new SharedContext(), null],
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
    public void primitive_numeric_parameters_use_numeric_schemas()
    {
        var operation = new OpenApiOperation();
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.OperationTransformer+RequestOperationTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext(), null],
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
            args: [new DocumentOptions(), sharedCtx, null],
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
