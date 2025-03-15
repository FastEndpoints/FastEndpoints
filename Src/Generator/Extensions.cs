#pragma warning disable IDE1006
using System.Text;
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

    internal static string Sanitize(this string input)
    {
        var result = new StringBuilder();
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    internal static ITypeSymbol GetUnderlyingType(this ITypeSymbol symbol)
    {
        if (symbol is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated })
            return symbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        if (symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && symbol is INamedTypeSymbol namedTypeSymbol)
            return namedTypeSymbol.TypeArguments[0];

        return symbol;
    }
}