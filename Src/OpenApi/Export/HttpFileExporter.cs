using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class HttpFileExporter
{
    static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public static string ToHttpFileContent(OpenApiDocument document, string documentName)
    {
        var sb = new StringBuilder();
        sb.Append("@baseUrl = https://localhost:5001\n\n");

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value is not { } pathItem)
                continue;

            foreach (var opEntry in pathItem.Operations ?? [])
                AppendOperation(sb, opEntry.Key.Method, pathEntry.Key, opEntry.Value);
        }

        return sb.ToString();
    }

    static void AppendOperation(StringBuilder sb, string method, string path, OpenApiOperation operation)
    {
        var parameters = operation.Parameters ?? [];
        var queryParams = parameters.Where(p => p.In == ParameterLocation.Query).ToList();
        var headerParams = parameters.Where(p => p.In == ParameterLocation.Header).ToList();

        sb.Append("### ").Append(operation.OperationId ?? $"{method} {path}").Append('\n');

        var url = "{{baseUrl}}" + ToRestClientPath(path);

        if (queryParams.Count > 0)
            url += "?" + string.Join('&', queryParams.Select(p => $"{p.Name}={{{{{p.Name}}}}}"));

        sb.Append(method).Append(' ').Append(url).Append('\n');

        foreach (var p in headerParams)
            sb.Append(p.Name).Append(": {{").Append(p.Name).Append("}}\n");

        var (contentType, mediaType) = ResolveRequestMediaType(operation.RequestBody);

        if (contentType is not null)
            sb.Append("Content-Type: ").Append(contentType).Append('\n');

        sb.Append("Accept: application/json\n\n");

        if (mediaType?.Schema is not null)
        {
            var placeholder = BuildPlaceholder(mediaType.Schema, []) ?? new JsonObject(); // never emit a literal 'null' body
            sb.Append(placeholder.ToJsonString(_jsonOpts)).Append('\n');
        }

        sb.Append('\n');
    }

    static (string? ContentType, OpenApiMediaType? MediaType) ResolveRequestMediaType(IOpenApiRequestBody? requestBody)
    {
        if (requestBody?.Content is not { Count: > 0 } content)
            return (null, null);

        if (content.TryGetValue("application/json", out var json))
            return ("application/json", json);

        var first = content.First();

        return (first.Key, first.Value);
    }

    static string ToRestClientPath(string path)
        => RouteParamRegex().Replace(path, "{{$1}}");

    // turns a schema into a placeholder JSON value keyed by property name, so the generated request body is fillable rather than an opaque '{}'.
    static JsonNode? BuildPlaceholder(IOpenApiSchema schema, HashSet<IOpenApiSchema> seen)
    {
        if (!seen.Add(schema))
            return null; // breaks cycles on self-referencing schemas

        if (schema.OneOf?.Count > 0 || schema.AnyOf?.Count > 0 || schema.AllOf?.Count > 0)
            return new JsonObject(); // composed/polymorphic schemas aren't walked, fall back to an empty object

        var type = schema.Type;

        if (type?.HasFlag(JsonSchemaType.Array) == true)
            return schema.Items is null ? new JsonArray() : new JsonArray(BuildPlaceholder(schema.Items, seen));

        if (schema.Properties is { Count: > 0 } props && (type is null || type.Value.HasFlag(JsonSchemaType.Object)))
        {
            var obj = new JsonObject();

            foreach (var (name, propSchema) in props)
                obj[name] = propSchema is null ? null : BuildPlaceholder(propSchema, seen);

            return obj;
        }

        if (type?.HasFlag(JsonSchemaType.String) == true)
        {
            return schema.Enum?.FirstOrDefault() is JsonValue enumValue && enumValue.TryGetValue<string>(out var s)
                       ? JsonValue.Create(s)
                       : JsonValue.Create("");
        }

        if (type?.HasFlag(JsonSchemaType.Integer) == true || type?.HasFlag(JsonSchemaType.Number) == true)
            return JsonValue.Create(0);

        if (type?.HasFlag(JsonSchemaType.Boolean) == true)
            return JsonValue.Create(false);

        return type?.HasFlag(JsonSchemaType.Object) == true ? new JsonObject() : null;
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex RouteParamRegex();
}
