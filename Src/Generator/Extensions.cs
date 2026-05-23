using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FastEndpoints.Generator;

static class Extensions
{
    // ReSharper disable once InconsistentNaming
    internal static StringBuilder w(this StringBuilder sb, string? val)
    {
        sb.Append(val);

        return sb;
    }

    // ReSharper disable once InconsistentNaming
    internal static StringBuilder l(this StringBuilder sb, string? val)
    {
        sb.AppendLine(val);

        return sb;
    }

    static readonly Regex _identifierRegex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);
    static readonly Regex _namespaceRegex = new("[^a-zA-Z0-9.]+", RegexOptions.Compiled);

    extension(string input)
    {
        internal string ToValidIdentifier(string replacement)
            => _identifierRegex.Replace(input, replacement);

        internal string ToValidCSharpIdentifier(string replacement)
        {
            var identifier = input.ToValidIdentifier(replacement);

            if (identifier.Length == 0 || char.IsDigit(identifier[0]) || SyntaxFacts.GetKeywordKind(identifier) is not SyntaxKind.None)
                identifier = $"_{identifier}";

            return identifier;
        }

        internal string ToValidNameSpace(string replacement = "_")
            => _namespaceRegex.Replace(input, replacement);
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
