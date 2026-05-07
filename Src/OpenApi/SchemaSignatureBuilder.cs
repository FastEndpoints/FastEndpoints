using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.DocumentSchemaHelpers;

namespace FastEndpoints.OpenApi;

static class SchemaSignatureBuilder
{
    internal static string GetSchemaSignature(string refId,
                                              IOpenApiSchema schema,
                                              Dictionary<string, string> aliases,
                                              Dictionary<SchemaSignatureCacheKey, string> signatureCache,
                                              int aliasRevision)
    {
        var cacheKey = new SchemaSignatureCacheKey(refId, aliasRevision);

        if (signatureCache.TryGetValue(cacheKey, out var cachedSignature))
            return cachedSignature;

        var builder = new StringBuilder();
        AppendSchemaSignature(builder, schema, aliases, []);

        var signature = builder.ToString();
        signatureCache[cacheKey] = signature;

        return signature;
    }

    static void AppendSchemaSignature(StringBuilder builder,
                                      IOpenApiSchema? schema,
                                      Dictionary<string, string> aliases,
                                      HashSet<IOpenApiSchema> visited)
    {
        switch (schema)
        {
            case null:
                builder.Append("null;");

                return;
            case OpenApiSchemaReference schemaRef:
                builder.Append("ref:").Append(schemaRef.GetReferenceId() is { } refId ? ResolveAlias(refId, aliases) : string.Empty).Append(';');

                return;
            case OpenApiSchema s:
                if (!visited.Add(s))
                {
                    builder.Append("cycle;");

                    return;
                }

                builder.Append("schema{");
                AppendValue(builder, "id", s.Id);
                AppendValue(builder, "title", s.Title);
                AppendValue(builder, "type", s.Type);
                AppendValue(builder, "format", s.Format);
                AppendValue(builder, "description", s.Description);
                AppendValue(builder, "comment", s.Comment);
                AppendValue(builder, "const", s.Const);
                AppendValue(builder, "exclusiveMaximum", s.ExclusiveMaximum);
                AppendValue(builder, "exclusiveMinimum", s.ExclusiveMinimum);
                AppendValue(builder, "maximum", s.Maximum);
                AppendValue(builder, "minimum", s.Minimum);
                AppendValue(builder, "maxLength", s.MaxLength);
                AppendValue(builder, "minLength", s.MinLength);
                AppendValue(builder, "pattern", s.Pattern);
                AppendValue(builder, "multipleOf", s.MultipleOf);
                AppendValue(builder, "readOnly", s.ReadOnly);
                AppendValue(builder, "writeOnly", s.WriteOnly);
                AppendValue(builder, "maxItems", s.MaxItems);
                AppendValue(builder, "minItems", s.MinItems);
                AppendValue(builder, "uniqueItems", s.UniqueItems);
                AppendValue(builder, "maxProperties", s.MaxProperties);
                AppendValue(builder, "minProperties", s.MinProperties);
                AppendValue(builder, "additionalPropertiesAllowed", s.AdditionalPropertiesAllowed);
                AppendValue(builder, "unevaluatedProperties", s.UnevaluatedProperties);
                AppendValue(builder, "deprecated", s.Deprecated);
                AppendJsonNode(builder, "default", s.Default);
                AppendJsonNode(builder, "example", s.Example);
                AppendJsonNodeList(builder, "examples", s.Examples);
                AppendJsonNodeList(builder, "enum", s.Enum);
                AppendStringSet(builder, "required", s.Required);
                AppendStringBoolDictionary(builder, "vocabulary", s.Vocabulary);
                AppendSchema(builder, "not", s.Not, aliases, visited);
                AppendSchema(builder, "items", s.Items, aliases, visited);
                AppendSchema(builder, "additionalProperties", s.AdditionalProperties, aliases, visited);
                AppendSchemaList(builder, "allOf", s.AllOf, aliases, visited);
                AppendSchemaList(builder, "oneOf", s.OneOf, aliases, visited);
                AppendSchemaList(builder, "anyOf", s.AnyOf, aliases, visited);
                AppendSchemaDictionary(builder, "properties", s.Properties, aliases, visited);
                AppendSchemaDictionary(builder, "patternProperties", s.PatternProperties, aliases, visited);
                AppendSchemaDictionary(builder, "definitions", s.Definitions, aliases, visited);
                AppendDiscriminator(builder, s.Discriminator, aliases, visited);
                AppendXml(builder, s.Xml);
                AppendExternalDocs(builder, s.ExternalDocs);
                AppendExtensions(builder, s.Extensions);
                AppendJsonNodeDictionary(builder, "unrecognizedKeywords", s.UnrecognizedKeywords);
                AppendDependentRequired(builder, s.DependentRequired);
                builder.Append('}');
                visited.Remove(s);

                return;
            default:
                builder.Append(schema.GetType().FullName).Append(';');

                return;
        }
    }

    static void AppendValue<T>(StringBuilder builder, string name, T value)
        => builder.Append(name).Append('=').Append(value?.ToString()).Append(';');

    static void AppendJsonNode(StringBuilder builder, string name, JsonNode? node)
    {
        builder.Append(name).Append('=');
        AppendJsonNodeValue(builder, node);
        builder.Append(';');
    }

    static void AppendJsonNodeValue(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");

                return;
            case JsonObject obj:
                builder.Append('{');

                foreach (var (key, value) in obj.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
                {
                    builder.Append(key).Append(':');
                    AppendJsonNodeValue(builder, value);
                    builder.Append(',');
                }

                builder.Append('}');

                return;
            case JsonArray arr:
                builder.Append('[');

                foreach (var value in arr)
                {
                    AppendJsonNodeValue(builder, value);
                    builder.Append(',');
                }

                builder.Append(']');

                return;
            default:
                builder.Append(node.ToJsonString());

                return;
        }
    }

    static void AppendJsonNodeList(StringBuilder builder, string name, IList<JsonNode>? nodes)
    {
        builder.Append(name).Append("=[");

        if (nodes is not null)
        {
            foreach (var node in nodes)
            {
                AppendJsonNodeValue(builder, node);
                builder.Append(',');
            }
        }

        builder.Append("]; ");
    }

    static void AppendStringSet(StringBuilder builder, string name, ISet<string>? values)
    {
        builder.Append(name).Append("=[");

        if (values is not null)
        {
            foreach (var value in values.Order(StringComparer.Ordinal))
                builder.Append(value).Append(',');
        }

        builder.Append("]; ");
    }

    static void AppendStringBoolDictionary(StringBuilder builder, string name, IDictionary<string, bool>? values)
    {
        builder.Append(name).Append("={");

        if (values is not null)
        {
            foreach (var (key, value) in values.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
                builder.Append(key).Append(':').Append(value).Append(',');
        }

        builder.Append("};");
    }

    static void AppendSchema(StringBuilder builder,
                             string name,
                             IOpenApiSchema? schema,
                             Dictionary<string, string> aliases,
                             HashSet<IOpenApiSchema> visited)
    {
        builder.Append(name).Append('=');
        AppendSchemaSignature(builder, schema, aliases, visited);
    }

    static void AppendSchemaList(StringBuilder builder,
                                 string name,
                                 IList<IOpenApiSchema>? schemas,
                                 Dictionary<string, string> aliases,
                                 HashSet<IOpenApiSchema> visited)
    {
        builder.Append(name).Append("=[");

        if (schemas is not null)
        {
            foreach (var schema in schemas)
            {
                AppendSchemaSignature(builder, schema, aliases, visited);
                builder.Append(',');
            }
        }

        builder.Append("]; ");
    }

    static void AppendSchemaDictionary(StringBuilder builder,
                                       string name,
                                       IDictionary<string, IOpenApiSchema>? schemas,
                                       Dictionary<string, string> aliases,
                                       HashSet<IOpenApiSchema> visited)
    {
        builder.Append(name).Append("={");

        if (schemas is not null)
        {
            foreach (var (key, schema) in schemas.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
            {
                builder.Append(key).Append(':');
                AppendSchemaSignature(builder, schema, aliases, visited);
                builder.Append(',');
            }
        }

        builder.Append("};");
    }

    static void AppendDiscriminator(StringBuilder builder,
                                    OpenApiDiscriminator? discriminator,
                                    Dictionary<string, string> aliases,
                                    HashSet<IOpenApiSchema> visited)
    {
        builder.Append("discriminator={");

        if (discriminator is not null)
        {
            AppendValue(builder, "propertyName", discriminator.PropertyName);

            if (discriminator.Mapping is not null)
            {
                foreach (var (key, schema) in discriminator.Mapping.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
                {
                    builder.Append(key).Append(':');
                    AppendSchemaSignature(builder, schema, aliases, visited);
                    builder.Append(',');
                }
            }
        }

        builder.Append("};");
    }

    static void AppendXml(StringBuilder builder, OpenApiXml? xml)
    {
        builder.Append("xml={");

        if (xml is not null)
        {
            AppendValue(builder, "name", xml.Name);
            AppendValue(builder, "namespace", xml.Namespace);
            AppendValue(builder, "prefix", xml.Prefix);
            AppendValue(builder, "attribute", xml.Attribute);
            AppendValue(builder, "wrapped", xml.Wrapped);
        }

        builder.Append("};");
    }

    static void AppendExternalDocs(StringBuilder builder, OpenApiExternalDocs? docs)
    {
        builder.Append("externalDocs={");

        if (docs is not null)
        {
            AppendValue(builder, "description", docs.Description);
            AppendValue(builder, "url", docs.Url);
        }

        builder.Append("};");
    }

    static void AppendExtensions(StringBuilder builder, IDictionary<string, IOpenApiExtension>? extensions)
    {
        builder.Append("extensions={");

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
                builder.Append(key).Append(':').Append(value.GetType().FullName).Append(',');
        }

        builder.Append("};");
    }

    static void AppendJsonNodeDictionary(StringBuilder builder, string name, IDictionary<string, JsonNode>? values)
    {
        builder.Append(name).Append("={");

        if (values is not null)
        {
            foreach (var (key, value) in values.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
            {
                builder.Append(key).Append(':');
                AppendJsonNodeValue(builder, value);
                builder.Append(',');
            }
        }

        builder.Append("};");
    }

    static void AppendDependentRequired(StringBuilder builder, IDictionary<string, HashSet<string>>? values)
    {
        builder.Append("dependentRequired={");

        if (values is not null)
        {
            foreach (var (key, set) in values.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
            {
                builder.Append(key).Append(':');

                foreach (var value in set.Order(StringComparer.Ordinal))
                    builder.Append(value).Append(',');
            }
        }

        builder.Append("};");
    }
}
