using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace FastEndpoints.Generator;

static class NamespaceExtensions
{
    static readonly Regex _identifierRegex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);
    static readonly Regex _namespaceRegex = new("[^a-zA-Z0-9.]+", RegexOptions.Compiled);

    extension(string input)
    {
        internal string ToValidIdentifier(string replacement = "_")
            => _identifierRegex.Replace(input, replacement);

        internal string ToValidCSharpIdentifier(string replacement = "_")
        {
            var identifier = input.ToValidIdentifier(replacement);

            if (identifier.Length == 0 || char.IsDigit(identifier[0]) || SyntaxFacts.GetKeywordKind(identifier) is not SyntaxKind.None)
                identifier = $"_{identifier}";

            return identifier;
        }

        internal string ToValidNameSpace(string replacement = "_")
            => string.Join(".", _namespaceRegex.Replace(input, replacement).Split('.').Select(s => s.ToValidCSharpIdentifier(replacement)));
    }
}
