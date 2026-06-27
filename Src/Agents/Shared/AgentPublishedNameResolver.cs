using System.Text;

namespace FastEndpoints.Agents;

static class AgentPublishedNameResolver
{
    const int MaxNameLength = 64;
    const string ValidNamePattern = "^[A-Za-z0-9_-]{1,64}$";
    const string ValidPathNamePattern = "^[A-Za-z0-9_./-]{1,64}$";

    internal static string Resolve(EndpointDefinition def,
                                   string? explicitValue,
                                   string valueLabel,
                                   string pluralValueLabel,
                                   bool allowPathSeparators = false)
    {
        var validNamePattern = allowPathSeparators ? ValidPathNamePattern : ValidNamePattern;

        if (explicitValue is not null)
            return ValidateExplicit(def.EndpointType, explicitValue, valueLabel, pluralValueLabel, validNamePattern, allowPathSeparators);

        var generatedSource = !string.IsNullOrWhiteSpace(def.EndpointSummary?.Summary)
                                  ? def.EndpointSummary!.Summary
                                  : def.EndpointType.Name;
        var normalizedValue = SanitizeGenerated(NamingHelpers.ToSnakeCase(generatedSource), allowPathSeparators);

        if (normalizedValue.Length == 0)
            throw new InvalidOperationException(
                $"Generated {valueLabel} for endpoint '{FormatEndpointType(def.EndpointType)}' is empty after normalization. Bad value: '{generatedSource}'. Set an explicit {valueLabel} matching {validNamePattern}.");

        return normalizedValue;
    }

    static string ValidateExplicit(Type endpointType,
                                   string value,
                                   string valueLabel,
                                   string pluralValueLabel,
                                   string validNamePattern,
                                   bool allowPathSeparators)
    {
        if (!IsValid(value, allowPathSeparators))
            throw new InvalidOperationException(
                $"Invalid explicit {valueLabel} '{value}' for endpoint '{FormatEndpointType(endpointType)}'. Explicit {pluralValueLabel} must match {validNamePattern}.");

        return value;
    }

    static string SanitizeGenerated(string value, bool allowPathSeparators)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var c in value)
        {
            if (IsValidChar(c, allowPathSeparators))
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

    static bool IsValid(string value, bool allowPathSeparators)
        => value.Length is > 0 and <= MaxNameLength && value.All(c => IsValidChar(c, allowPathSeparators));

    static bool IsValidChar(char c, bool allowPathSeparators)
        => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' || (allowPathSeparators && (c is '.' or '/'));

    static string FormatEndpointType(Type endpointType)
        => endpointType.FullName ?? endpointType.Name;
}
