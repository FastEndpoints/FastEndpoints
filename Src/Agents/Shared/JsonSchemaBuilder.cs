using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace FastEndpoints.Agents;

/// <summary>
/// builds JSON-Schema nodes from CLR types using <see cref="JsonSchemaExporter" />. results are cached
/// per <c>(Type, JsonSerializerOptions)</c> pair because schema generation is reflective and not cheap.
/// enrichment with FluentValidation constraints is applied by <see cref="FluentValidationSchemaEnricher" />
/// on top of the base schema.
/// </summary>
static class JsonSchemaBuilder
{
    static readonly ConcurrentDictionary<(Type, JsonSerializerOptions), JsonNode> _cache = new();

    /// <summary>
    /// generates a JSON-Schema document for <paramref name="type" /> honoring <paramref name="options" />'s
    /// property naming, number handling, and converter configuration.
    /// </summary>
    /// <param name="type">the CLR type to describe.</param>
    /// <param name="options">the serializer options whose <see cref="JsonSerializerOptions.TypeInfoResolver" /> is used.</param>
    /// <returns>a fresh clone of the cached schema node — callers may mutate it freely.</returns>
    public static JsonNode Build(Type type, JsonSerializerOptions options)
    {
        var cached = _cache.GetOrAdd(
            (type, options),
            static key =>
            {
                var serializerOptions = key.Item2.TypeInfoResolver is null
                                            ? new JsonSerializerOptions(key.Item2) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }
                                            : key.Item2;

                return JsonSchemaExporter.GetJsonSchemaAsNode(serializerOptions, key.Item1);
            });

        return cached.DeepClone();
    }
}
