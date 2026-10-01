using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

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
    /// <returns>a fresh clone of the cached schema node. callers may mutate it freely.</returns>
    public static JsonNode Build(Type type, JsonSerializerOptions options)
    {
        var cached = _cache.GetOrAdd(
            (type, options),
            static key =>
            {
                var serializerOptions = AgentJsonSerializerOptions.EnsureTypeInfoResolver(key.Item2);

                return serializerOptions.GetJsonSchemaAsNode(
                    key.Item1,
                    new()
                    {
                        TransformSchemaNode = static (context, schema) =>
                                              {
                                                  if (context.PropertyInfo is not { } jsonProperty)
                                                      return schema;

                                                  // Source-generated metadata does not populate AttributeProvider.
                                                  var attributeProvider = jsonProperty.AttributeProvider ??
                                                                          jsonProperty.DeclaringType?.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                                                                      .FirstOrDefault(
                                                                                          p => (p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                                                                                                context.TypeInfo.Options.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name) ==
                                                                                               jsonProperty.Name);

                                                  if (attributeProvider?.GetCustomAttributes(typeof(DescriptionAttribute), true)
                                                                       .OfType<DescriptionAttribute>()
                                                                       .FirstOrDefault() is not { Description: { Length: > 0 } description })
                                                      return schema;

                                                  if (schema is JsonValue)
                                                      schema = schema.GetValue<bool>() ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };

                                                  schema["description"] = description;

                                                  return schema;
                                              }
                    });
            });

        return cached.DeepClone();
    }
}