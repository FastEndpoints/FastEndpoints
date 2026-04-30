using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer
{
    sealed partial class RequestOperationTransformer
    {
        JsonNode? BuildRequestExampleFallback(EndpointDefinition epDef,
                                              HashSet<string> propsRemovedFromBody,
                                              PromotedBodyProperty? promotedBodyProperty)
        {
            var fallback = (promotedBodyProperty?.Type ?? GetRequestDtoType(epDef))?.GenerateSampleJsonNode(NamingPolicy, docOpts.UsePropertyNamingPolicy);

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

        static JsonNode? BuildRequestExampleNode(object? example,
                                                 HashSet<string> propsRemovedFromBody,
                                                 PromotedBodyProperty? promotedBodyProperty)
        {
            var node = example.JsonNodeFromObject();

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

        static JsonNode? NormalizeExampleNode(JsonNode? example, OpenApiSchema? schema, JsonNode? fallback)
        {
            if (example is null)
                return AllowsNull(schema) ? null : fallback?.DeepClone() ?? CreateSampleFromSchema(schema);

            if (schema?.Enum is { Count: > 0 } enumValues && !MatchesEnumValue(example, enumValues))
                return enumValues[0]?.DeepClone();

            return example switch
            {
                JsonObject obj => NormalizeObjectExample(obj, schema, fallback as JsonObject),
                JsonArray arr => NormalizeArrayExample(arr, schema, fallback as JsonArray),
                _ => example
            };
        }

        static JsonObject NormalizeObjectExample(JsonObject example, OpenApiSchema? schema, JsonObject? fallback)
        {
            if (schema?.Properties is not { Count: > 0 } properties)
                return example;

            var propertyKeys = new Dictionary<string, string>(properties.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var propertyKey in properties.Keys)
                propertyKeys.TryAdd(propertyKey, propertyKey);

            Dictionary<string, string>? fallbackKeys = null;

            if (fallback is not null)
            {
                fallbackKeys = new(fallback.Count, StringComparer.OrdinalIgnoreCase);

                foreach (var fallbackKey in fallback.Select(static kvp => kvp.Key))
                    fallbackKeys.TryAdd(fallbackKey, fallbackKey);
            }

            foreach (var key in example.Select(kvp => kvp.Key).ToArray())
            {
                if (!propertyKeys.TryGetValue(key, out var schemaKey) || properties[schemaKey].ResolveSchema() is not { } propertySchema)
                    continue;

                string? fallbackKey = null;
                fallbackKeys?.TryGetValue(key, out fallbackKey);
                var fallbackNode = fallbackKey is not null ? fallback![fallbackKey] : null;

                example[key] = NormalizeExampleNode(example[key], propertySchema, fallbackNode);
            }

            return example;
        }

        static JsonArray NormalizeArrayExample(JsonArray example, OpenApiSchema? schema, JsonArray? fallback)
        {
            var itemSchema = schema?.Items.ResolveSchema();

            if (itemSchema is null)
                return example;

            var fallbackNode = fallback is { Count: > 0 } ? fallback[0] : null;

            for (var i = 0; i < example.Count; i++)
                example[i] = NormalizeExampleNode(example[i], itemSchema, fallbackNode);

            return example;
        }

        static bool AllowsNull(OpenApiSchema? schema)
        {
            if (schema is null)
                return true;

            if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null))
                return true;

            if (schema.OneOf?.Any(s => AllowsNull(s.ResolveSchema())) == true)
                return true;

            return schema.AnyOf?.Any(s => AllowsNull(s.ResolveSchema())) == true;
        }

        static JsonNode? CreateSampleFromSchema(OpenApiSchema? schema, string? propertyName = null)
        {
            if (schema is null)
                return null;

            if (schema.Enum is { Count: > 0 })
                return schema.Enum[0]?.DeepClone();

            if (schema.OneOf is { Count: > 0 })
            {
                foreach (var option in schema.OneOf)
                {
                    var resolved = option.ResolveSchema();

                    if (resolved is not null && !AllowsNull(resolved))
                        return CreateSampleFromSchema(resolved, propertyName);
                }
            }

            if (schema.AnyOf is { Count: > 0 })
            {
                foreach (var option in schema.AnyOf)
                {
                    var resolved = option.ResolveSchema();

                    if (resolved is not null && !AllowsNull(resolved))
                        return CreateSampleFromSchema(resolved, propertyName);
                }
            }

            if (schema.Properties is { Count: > 0 })
            {
                var obj = new JsonObject();

                foreach (var (key, propertySchema) in schema.Properties)
                {
                    var sample = CreateSampleFromSchema(propertySchema.ResolveSchema(), key);

                    if (sample is not null)
                        obj[key] = sample;
                }

                return obj.Count > 0 ? obj : null;
            }

            if (schema.AdditionalProperties.ResolveSchema() is { } additionalPropertiesSchema)
            {
                var sample = CreateSampleFromSchema(additionalPropertiesSchema, "additionalProp1");

                return sample is not null
                           ? new JsonObject { ["additionalProp1"] = sample }
                           : new JsonObject();
            }

            if (schema.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemSample = CreateSampleFromSchema(schema.Items.ResolveSchema(), propertyName);

                return itemSample is not null ? new JsonArray(itemSample) : new JsonArray();
            }

            return schema.Type switch
            {
                JsonSchemaType.String => (propertyName ?? string.Empty).JsonNodeFromObject(),
                JsonSchemaType.Integer => 0.JsonNodeFromObject(),
                JsonSchemaType.Number => 0m.JsonNodeFromObject(),
                JsonSchemaType.Boolean => false.JsonNodeFromObject(),
                _ => null
            };
        }

        static bool MatchesEnumValue(JsonNode example, IList<JsonNode> enumValues)
        {
            var exampleJson = example.ToJsonString();

            for (var i = 0; i < enumValues.Count; i++)
            {
                if (enumValues[i]?.ToJsonString() == exampleJson)
                    return true;
            }

            return false;
        }
    }
}
