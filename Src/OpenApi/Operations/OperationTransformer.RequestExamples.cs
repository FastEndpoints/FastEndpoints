using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace FastEndpoints.OpenApi;

sealed partial class RequestOperationTransformer
{
        JsonNode? BuildRequestExampleFallback(EndpointDefinition epDef,
                                               HashSet<string> propsRemovedFromBody,
                                               PromotedBodyProperty? promotedBodyProperty)
        {
            var fallback = (promotedBodyProperty?.Type ?? epDef.ReqDtoType).GenerateSampleJsonNode(
                SerializerOptions,
                NamingPolicy,
                docOpts.UsePropertyNamingPolicy);

            if (promotedBodyProperty is null && fallback is JsonObject fallbackObj && propsRemovedFromBody.Count > 0)
                fallbackObj.RemoveProperties(propsRemovedFromBody);

            return fallback;
        }

        JsonNode? GetRequestExampleFallback(EndpointDefinition epDef,
                                            RequestTransformState state,
                                            PromotedBodyProperty? promotedBodyProperty)
        {
            if (!state.RequestBodyFallbackExampleCreated)
            {
                state.RequestBodyFallbackExample = BuildRequestExampleFallback(epDef, state.PropsRemovedFromBody, promotedBodyProperty);
                state.RequestBodyFallbackExampleCreated = true;
            }

            return state.RequestBodyFallbackExample;
        }

        JsonNode? BuildRequestExampleNode(object? example,
                                          HashSet<string> propsRemovedFromBody,
                                          PromotedBodyProperty? promotedBodyProperty)
        {
            var node = example.JsonNodeFromObject(SerializerOptions);

            if (promotedBodyProperty is not null)
                return UnwrapPromotedBodyExample(node, promotedBodyProperty.Name);

            return StripRemovedProps(node, propsRemovedFromBody);
        }

        static JsonNode? UnwrapPromotedBodyExample(JsonNode? node, string promotedPropertyName)
        {
            if (node is not JsonObject obj)
                return node;

            var key = obj.Select(static kvp => kvp.Key).FindCaseInsensitiveKey(promotedPropertyName);

            return key is null ? node : obj[key]?.DeepClone();
        }

        static JsonNode? StripRemovedProps(JsonNode? node, HashSet<string> removedProps)
        {
            if (removedProps.Count == 0 || node is not JsonObject obj)
                return node;

            obj.RemoveProperties(removedProps);

            return obj;
        }

        static List<RequestExample> BuildUniqueRequestExamples(ICollection<RequestExample> examples)
        {
            var labelCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var example in examples)
            {
                labelCounts.TryGetValue(example.Label, out var count);
                labelCounts[example.Label] = count + 1;
            }

            var duplicateCounters = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new List<RequestExample>(examples.Count);

            foreach (var example in examples)
            {
                if (labelCounts[example.Label] == 1)
                {
                    result.Add(example);

                    continue;
                }

                duplicateCounters.TryGetValue(example.Label, out var duplicateIndex);
                duplicateIndex++;
                duplicateCounters[example.Label] = duplicateIndex;

                result.Add(new(example.Value, $"{example.Label} {duplicateIndex}", example.Summary, example.Description));
            }

            return result;
        }

        JsonNode? NormalizeExampleNode(JsonNode? example, OpenApiSchema? schema, JsonNode? fallback, OpenApiGenerationState generation)
        {
            if (example is null)
                return AllowsNull(schema, generation) ? null : fallback?.DeepClone() ?? CreateSampleFromSchema(schema, generation);

            if (schema?.Enum is { Count: > 0 } enumValues && !MatchesEnumValue(example, enumValues))
                return enumValues[0].DeepClone();

            return example switch
            {
                JsonObject obj => NormalizeObjectExample(obj, schema, fallback as JsonObject, generation),
                JsonArray arr => NormalizeArrayExample(arr, schema, fallback as JsonArray, generation),
                _ => example
            };
        }

        JsonObject NormalizeObjectExample(JsonObject example, OpenApiSchema? schema, JsonObject? fallback, OpenApiGenerationState generation)
        {
            if (schema?.Properties is not { Count: > 0 } properties)
                return example;

            var propertyKeys = properties.Keys.Select(static key => KeyValuePair.Create(key, key))
                                             .ToCaseInsensitiveDictionary(properties.Count);

            Dictionary<string, string>? fallbackKeys = null;

            if (fallback is not null)
                fallbackKeys = fallback.Select(static kvp => KeyValuePair.Create(kvp.Key, kvp.Key))
                                       .ToCaseInsensitiveDictionary(fallback.Count);

            foreach (var key in example.Select(kvp => kvp.Key).ToArray())
            {
                if (!propertyKeys.TryGetValue(key, out var schemaKey) || properties[schemaKey].ResolveSchema(generation) is not { } propertySchema)
                    continue;

                string? fallbackKey = null;
                fallbackKeys?.TryGetValue(key, out fallbackKey);
                var fallbackNode = fallbackKey is not null ? fallback![fallbackKey] : null;

                var currentNode = example[key];
                var normalizedNode = NormalizeExampleNode(currentNode, propertySchema, fallbackNode, generation);

                if (!ReferenceEquals(currentNode, normalizedNode))
                    example[key] = normalizedNode;
            }

            return example;
        }

        JsonArray NormalizeArrayExample(JsonArray example, OpenApiSchema? schema, JsonArray? fallback, OpenApiGenerationState generation)
        {
            var itemSchema = schema?.Items.ResolveSchema(generation);

            if (itemSchema is null)
                return example;

            var fallbackNode = fallback is { Count: > 0 } ? fallback[0] : null;

            for (var i = 0; i < example.Count; i++)
            {
                var currentNode = example[i];
                var normalizedNode = NormalizeExampleNode(currentNode, itemSchema, fallbackNode, generation);

                if (!ReferenceEquals(currentNode, normalizedNode))
                    example[i] = normalizedNode;
            }

            return example;
        }

        bool AllowsNull(OpenApiSchema? schema, OpenApiGenerationState generation)
        {
            if (schema is null)
                return true;

            if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null))
                return true;

            return ContainsNullOption(schema.OneOf, generation) || ContainsNullOption(schema.AnyOf, generation);
        }

        JsonNode? CreateSampleFromSchema(OpenApiSchema? schema, OpenApiGenerationState generation, string? propertyName = null)
        {
            if (schema is null)
                return null;

            if (schema.Enum is { Count: > 0 })
                return schema.Enum[0].DeepClone();

            if (CreateSampleFromFirstNonNullableOption(schema.OneOf, generation, propertyName) is { } oneOfSample)
                return oneOfSample;

            if (CreateSampleFromFirstNonNullableOption(schema.AnyOf, generation, propertyName) is { } anyOfSample)
                return anyOfSample;

            if (schema.Properties is { Count: > 0 })
            {
                var obj = new JsonObject();

                foreach (var (key, propertySchema) in schema.Properties)
                {
                    var sample = CreateSampleFromSchema(propertySchema.ResolveSchema(generation), generation, key);

                    if (sample is not null)
                        obj[key] = sample;
                }

                return obj.Count > 0 ? obj : null;
            }

            if (schema.AdditionalProperties.ResolveSchema(generation) is { } additionalPropertiesSchema)
            {
                var sample = CreateSampleFromSchema(additionalPropertiesSchema, generation, "additionalProp1");

                return sample is not null
                           ? new JsonObject { ["additionalProp1"] = sample }
                           : new JsonObject();
            }

            if (schema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemSample = CreateSampleFromSchema(schema.Items.ResolveSchema(generation), generation, propertyName);

                return itemSample is not null ? new JsonArray(itemSample) : new JsonArray();
            }

            return schema.Type switch
            {
                JsonSchemaType.String => (propertyName ?? string.Empty).JsonNodeFromObject(SerializerOptions),
                JsonSchemaType.Integer => 0.JsonNodeFromObject(SerializerOptions),
                JsonSchemaType.Number => 0m.JsonNodeFromObject(SerializerOptions),
                JsonSchemaType.Boolean => false.JsonNodeFromObject(SerializerOptions),
                _ => null
            };
        }

        static bool MatchesEnumValue(JsonNode example, IList<JsonNode> enumValues)
        {
            var exampleJson = example.ToJsonString();

            for (var i = 0; i < enumValues.Count; i++)
            {
                if (enumValues[i].ToJsonString() == exampleJson)
                    return true;
            }

            return false;
        }

        bool ContainsNullOption(IList<IOpenApiSchema>? schemas, OpenApiGenerationState generation)
            => schemas?.Any(s => AllowsNull(s.ResolveSchema(generation), generation)) == true;

        JsonNode? CreateSampleFromFirstNonNullableOption(IList<IOpenApiSchema>? schemas, OpenApiGenerationState generation, string? propertyName)
        {
            if (schemas is not { Count: > 0 })
                return null;

            foreach (var option in schemas)
            {
                var resolved = option.ResolveSchema(generation);

                if (resolved is not null && !AllowsNull(resolved, generation))
                    return CreateSampleFromSchema(resolved, generation, propertyName);
            }

            return null;
        }
}
