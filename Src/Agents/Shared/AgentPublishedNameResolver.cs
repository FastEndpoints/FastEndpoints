using System.Text;

namespace FastEndpoints.Agents;

static class AgentPublishedNameResolver
{
    const int MaxNameLength = 64;
    const string ValidNamePattern = "^[A-Za-z0-9_./-]{1,64}$";

    internal static string Resolve(EndpointDefinition def,
                                   string? explicitValue,
                                   string valueLabel,
                                   string pluralValueLabel)
    {
        if (explicitValue is not null)
            return ValidateExplicit(def.EndpointType, explicitValue, valueLabel, pluralValueLabel);

        var generatedSource = !string.IsNullOrWhiteSpace(def.EndpointSummary?.Summary)
                                  ? def.EndpointSummary!.Summary
                                  : def.EndpointType.Name;
        var normalizedValue = SanitizeGenerated(NamingHelpers.ToSnakeCase(generatedSource));

        if (normalizedValue.Length == 0)
            throw new InvalidOperationException(
                $"Generated {valueLabel} for endpoint '{FormatEndpointType(def.EndpointType)}' is empty after normalization. Bad value: '{generatedSource}'. Set an explicit {valueLabel} matching {ValidNamePattern}.");

        return normalizedValue;
    }

    static string ValidateExplicit(Type endpointType,
                                   string value,
                                   string valueLabel,
                                   string pluralValueLabel)
    {
        if (!IsValid(value))
            throw new InvalidOperationException(
                $"Invalid explicit {valueLabel} '{value}' for endpoint '{FormatEndpointType(endpointType)}'. Explicit {pluralValueLabel} must match {ValidNamePattern}.");

        return value;
    }

    static string SanitizeGenerated(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var c in value)
        {
            if (IsValidChar(c))
            {
                var isSeparator = c is '_' or '-';

                if (isSeparator && (builder.Length == 0 || previousWasSeparator))
                    continue;

                builder.Append(c);
                previousWasSeparator = isSeparator;

                if (builder.Length == MaxNameLength)
                    break;

                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
                continue;

            builder.Append('_');
            previousWasSeparator = true;

            if (builder.Length == MaxNameLength)
                break;
        }

        if (builder.Length > 0 && builder[^1] is '_' or '-')
            builder.Length--;

        return builder.ToString();
    }

    static bool IsValid(string value)
        => value.Length is > 0 and <= MaxNameLength && value.All(IsValidChar);

    static bool IsValidChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '/';

    static string FormatEndpointType(Type endpointType)
        => endpointType.FullName ?? endpointType.Name;
}
