using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace FastEndpoints.Generator;

static class Extensions
{
    // ReSharper disable once InconsistentNaming
    internal static StringBuilder w(this StringBuilder sb, string? val)
    {
        sb.Append(val);

        return sb;
    }

    static readonly Regex _regex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    internal static string Sanitize(this string input, string replacement = "_")
        => _regex.Replace(input, replacement);

    internal static ITypeSymbol GetUnderlyingType(this ITypeSymbol symbol)
    {
        if (symbol is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated })
            return symbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        if (symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && symbol is INamedTypeSymbol namedTypeSymbol)
            return namedTypeSymbol.TypeArguments[0];

        return symbol;
    }
}