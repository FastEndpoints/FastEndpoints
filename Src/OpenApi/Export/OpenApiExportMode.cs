using Microsoft.Extensions.Configuration;

namespace FastEndpoints.OpenApi;

/// <summary>
/// owns export-mode config keys and predicate logic for JSON / '.http' / any format.
/// </summary>
static class OpenApiExportMode
{
    internal const string JsonExportKey = "export-openapi-docs";
    internal const string HttpExportKey = "export-http-files";

    internal static bool IsJson(IConfiguration config)
        => IsEnabled(config, JsonExportKey);

    internal static bool IsHttp(IConfiguration config)
        => IsEnabled(config, HttpExportKey);

    internal static bool IsAny(IConfiguration config)
        => IsJson(config) || IsHttp(config);

    static bool IsEnabled(IConfiguration config, string key)
        => string.Equals(config[key], "true", StringComparison.Ordinal);
}
