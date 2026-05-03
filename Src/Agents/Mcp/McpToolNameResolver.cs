using System.Text;
using FastEndpoints.Agents;

namespace FastEndpoints.Mcp;

static class McpToolNameResolver
{
    const string ValidNamePattern = "^[A-Za-z0-9_-]+$";

    internal static string ResolvePublishedName(EndpointDefinition def, McpToolInfo info)
    {
        if (info.Name is not null)
            return ValidateExplicitName(def.EndpointType, info.Name);

        var generatedSource = !string.IsNullOrWhiteSpace(def.EndpointSummary?.Summary)
                                  ? def.EndpointSummary!.Summary
                                  : def.EndpointType.Name;
        var normalizedName = SanitizeGeneratedName(NamingHelpers.ToSnakeCase(generatedSource));

        if (normalizedName.Length == 0)
            throw new InvalidOperationException(
                $"Generated MCP tool name for endpoint '{FormatEndpointType(def.EndpointType)}' is empty after normalization. Bad value: '{generatedSource}'. Set an explicit MCP tool name matching {ValidNamePattern}.");

        return normalizedName;
    }

    static string ValidateExplicitName(Type endpointType, string name)
    {
        if (!IsValidName(name))
            throw new InvalidOperationException(
                $"Invalid explicit MCP tool name '{name}' for endpoint '{FormatEndpointType(endpointType)}'. Explicit MCP tool names must match {ValidNamePattern}.");

        return name;
    }

    static string SanitizeGeneratedName(string name)
    {
        var builder = new StringBuilder(name.Length);
        var previousWasSeparator = false;

        foreach (var c in name)
        {
            if (IsNameChar(c))
            {
                var isSeparator = c is '_' or '-';

                if (isSeparator && (builder.Length == 0 || previousWasSeparator))
                    continue;

                builder.Append(c);
                previousWasSeparator = isSeparator;

                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
                continue;

            builder.Append('_');
            previousWasSeparator = true;
        }

        if (builder.Length > 0 && builder[^1] is '_' or '-')
            builder.Length--;

        return builder.ToString();
    }

    static bool IsValidName(string name)
        => name.Length != 0 && name.All(IsNameChar);

    static bool IsNameChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '_' or '-';

    static string FormatEndpointType(Type endpointType)
        => endpointType.FullName ?? endpointType.Name;
}
