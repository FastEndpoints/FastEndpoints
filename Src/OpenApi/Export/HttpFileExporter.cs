using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class HttpFileExporter
{
    static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    const string DefaultBaseUrl = "https://localhost:5001";

    public static string ToHttpFileContent(OpenApiDocument document)
    {
        var sb = new StringBuilder();
        sb.Append("@baseUrl = ").Append(ResolveBaseUrl(document)).Append("\n\n");
        var components = document.Components?.Schemas;

        foreach (var pathEntry in document.Paths)
        {
            if (pathEntry.Value is not { } pathItem)
                continue;

            foreach (var opEntry in pathItem.Operations ?? [])
                AppendOperation(sb, document, opEntry.Key.Method, pathEntry.Key, opEntry.Value, components);
        }

        return sb.ToString();
    }

    static string ResolveBaseUrl(OpenApiDocument document)
    {
        var serverUrl = document.Servers?.FirstOrDefault()?.Url;

        return string.IsNullOrWhiteSpace(serverUrl) ? DefaultBaseUrl : serverUrl.TrimEnd('/');
    }

    static void AppendOperation(StringBuilder sb, OpenApiDocument document, string method, string path, OpenApiOperation operation, IDictionary<string, IOpenApiSchema>? components)
    {
        var parameters = operation.Parameters ?? [];
        var queryParams = parameters.Where(p => p.In == ParameterLocation.Query).ToList();
        var headerParams = parameters.Where(p => p.In == ParameterLocation.Header).ToList();
        var cookieParams = parameters.Where(p => p.In == ParameterLocation.Cookie).ToList();

        sb.Append("### ").Append(operation.OperationId ?? $"{method} {path}").Append('\n');

        var url = "{{baseUrl}}" + ToRestClientPath(path);

        if (queryParams.Count > 0)
            url += "?" + string.Join('&', queryParams.Select(p => $"{p.Name}={{{{{p.Name}}}}}"));

        sb.Append(method).Append(' ').Append(url).Append('\n');

        foreach (var p in headerParams)
            sb.Append(p.Name).Append(": {{").Append(p.Name).Append("}}\n");

        if (cookieParams.Count > 0)
        {
            var cookieHeader = string.Join("; ", cookieParams.Select(p => $"{p.Name}={{{{{p.Name}}}}}"));
            sb.Append("Cookie: ").Append(cookieHeader).Append('\n');
        }

        if (!HasAuthorizationHeader(headerParams) && RequiresBearerToken(operation, document))
            sb.Append("Authorization: Bearer {{bearerToken}}\n");

        var (contentType, mediaType) = ResolveRequestMediaType(operation.RequestBody);

        if (contentType is not null)
            sb.Append("Content-Type: ").Append(contentType).Append('\n');

        sb.Append("Accept: application/json\n\n");

        AppendRequestBody(sb, contentType, mediaType, components);

        sb.Append('\n');
    }

    static void AppendRequestBody(StringBuilder sb, string? contentType, OpenApiMediaType? mediaType, IDictionary<string, IOpenApiSchema>? components)
    {
        if (contentType is null || mediaType is null)
            return;

        if (IsJsonContentType(contentType))
        {
            // media-type example is authoritative (full replace); else schema Example/Default via builder; never emit literal null root
            var node = mediaType.Example?.DeepClone() ??
                       FirstNamedExample(mediaType.Examples) ??
                       (mediaType.Schema is null
                            ? null
                            : SchemaPlaceholderBuilder.Build(mediaType.Schema, [], components));

            sb.Append((node ?? new JsonObject()).ToJsonString(_jsonOpts)).Append('\n');

            return;
        }

        if (IsFormContentType(contentType))
        {
            // REST Client has no first-class form body syntax; omit misleading JSON skeleton.
            sb.Append("# body omitted (").Append(contentType).Append("); provide form fields in the client\n");

            return;
        }

        // text/plain and other non-JSON media types
        sb.Append("{{body}}\n");
    }

    // first named example with a non-null Value (insertion order); ExternalValue is ignored in v1
    static JsonNode? FirstNamedExample(IDictionary<string, IOpenApiExample>? examples)
    {
        if (examples is null)
            return null;

        foreach (var ex in examples.Values)
        {
            if (ex.Value is not null)
                return ex.Value.DeepClone();
        }

        return null;
    }

    static bool IsJsonContentType(string contentType)
        => contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
           contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    static bool IsFormContentType(string contentType)
        => contentType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
           contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

    static bool HasAuthorizationHeader(List<IOpenApiParameter> headerParams)
        => headerParams.Any(p => string.Equals(p.Name, "Authorization", StringComparison.OrdinalIgnoreCase));

    static bool RequiresBearerToken(OpenApiOperation operation, OpenApiDocument document)
    {
        var requirements = operation.Security is { Count: > 0 }
                               ? operation.Security
                               : document.Security;

        if (requirements is not { Count: > 0 })
            return false;

        var schemes = document.Components?.SecuritySchemes;

        foreach (var requirement in requirements)
        {
            foreach (var schemeRef in requirement.Keys)
            {
                if (IsBearerScheme(schemeRef, schemes))
                    return true;
            }
        }

        return false;
    }

    static bool IsBearerScheme(OpenApiSecuritySchemeReference schemeRef, IDictionary<string, IOpenApiSecurityScheme>? schemes)
    {
        // OpenApiSecuritySchemeReference proxies Type/Scheme from Target when resolved
        if (schemeRef.Type == SecuritySchemeType.Http &&
            string.Equals(schemeRef.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return true;

        var schemeName = schemeRef.Reference?.Id;

        if (!string.IsNullOrEmpty(schemeName) &&
            schemes?.TryGetValue(schemeName, out var componentScheme) == true &&
            componentScheme is OpenApiSecurityScheme concrete &&
            IsHttpBearer(concrete))
            return true;

        // known JWT/bearer scheme names when scheme definition is missing
        return !string.IsNullOrEmpty(schemeName) &&
               (schemeName.Contains("JWT", StringComparison.OrdinalIgnoreCase) ||
                schemeName.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
    }

    static bool IsHttpBearer(OpenApiSecurityScheme scheme)
        => scheme.Type == SecuritySchemeType.Http &&
           string.Equals(scheme.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase);

    static (string? ContentType, OpenApiMediaType? MediaType) ResolveRequestMediaType(IOpenApiRequestBody? requestBody)
    {
        if (requestBody?.Content is not { Count: > 0 } content)
            return (null, null);

        if (content.TryGetValue("application/json", out var json))
            return ("application/json", json);

        // prefer vendor/structured JSON (e.g. application/json-patch+json) over form/text when both exist
        var plusJson = content.FirstOrDefault(static kv => kv.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

        if (plusJson.Key is not null)
            return (plusJson.Key, plusJson.Value);

        var first = content.First();

        return (first.Key, first.Value);
    }

    static string ToRestClientPath(string path)
        => RouteParamRegex().Replace(path, "{{$1}}");

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex RouteParamRegex();
}