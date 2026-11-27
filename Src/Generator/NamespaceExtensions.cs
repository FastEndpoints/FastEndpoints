using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace FastEndpoints.Generator;

static class NamespaceExtensions
{
    static readonly Regex _identifierRegex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    extension(string input)
    {
        internal string ToValidIdentifier(string replacement = "_")
            => _identifierRegex.Replace(input, replacement);

        //suffix of the generated public extension methods (AddFrom*, RegisterServicesFrom*, AddSerializerContextsFrom*). changing it renames user facing apis.
        internal string ToMethodSuffix()
            => input.ToValidIdentifier(string.Empty);

        internal string ToValidCSharpIdentifier()
        {
            var identifier = input.ToValidIdentifier();

            if (identifier.Length == 0 || char.IsDigit(identifier[0]) || SyntaxFacts.GetKeywordKind(identifier) is not SyntaxKind.None)
                identifier = $"_{identifier}";

            return identifier;
        }

        internal string ToValidNameSpace()
            => string.Join(".", input.Split('.').Select(static s => s.ToValidCSharpIdentifier()));
    }
}
