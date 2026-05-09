using System.Text;
using FastEndpoints.Agents;

namespace FastEndpoints.A2A;

static class A2ASkillIdResolver
{
    const int MaxIdLength = 64;
    const string ValidIdPattern = "^[A-Za-z0-9_-]{1,64}$";

    internal static string ResolvePublishedId(EndpointDefinition def, A2ASkillInfo info)
    {
        if (info.Id is not null)
            return ValidateExplicitId(def.EndpointType, info.Id);

        var generatedSource = !string.IsNullOrWhiteSpace(def.EndpointSummary?.Summary)
                                  ? def.EndpointSummary!.Summary
                                  : def.EndpointType.Name;
        var normalizedId = SanitizeGeneratedId(NamingHelpers.ToSnakeCase(generatedSource));

        if (normalizedId.Length == 0)
            throw new InvalidOperationException(
                $"Generated A2A skill id for endpoint '{FormatEndpointType(def.EndpointType)}' is empty after normalization. Bad value: '{generatedSource}'. Set an explicit A2A skill id matching {ValidIdPattern}.");

        return normalizedId;
    }

    static string ValidateExplicitId(Type endpointType, string id)
    {
        if (!IsValidId(id))
            throw new InvalidOperationException(
                $"Invalid explicit A2A skill id '{id}' for endpoint '{FormatEndpointType(endpointType)}'. Explicit A2A skill ids must match {ValidIdPattern}.");

        return id;
    }

    static string SanitizeGeneratedId(string id)
    {
        var builder = new StringBuilder(id.Length);
        var previousWasSeparator = false;

        foreach (var c in id)
        {
            if (IsIdChar(c))
            {
                var isSeparator = c is '_' or '-';

                if (isSeparator && (builder.Length == 0 || previousWasSeparator))
                    continue;

                builder.Append(c);
                previousWasSeparator = isSeparator;

                if (builder.Length == MaxIdLength)
                    break;

                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
                continue;

            builder.Append('_');
            previousWasSeparator = true;

            if (builder.Length == MaxIdLength)
                break;
        }

        if (builder.Length > 0 && builder[^1] is '_' or '-')
            builder.Length--;

        return builder.ToString();
    }

    static bool IsValidId(string id)
        => id.Length is > 0 and <= MaxIdLength && id.All(IsIdChar);

    static bool IsIdChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '_' or '-';

    static string FormatEndpointType(Type endpointType)
        => endpointType.FullName ?? endpointType.Name;
}
