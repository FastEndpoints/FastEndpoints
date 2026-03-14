using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static (HashSet<string> SerializableTypes, int EndpointCount) DiscoverSerializableTypesFromEndpoints(
        List<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        AnalysisContext analysis)
    {
        var serializableTypes = new HashSet<string>(StringComparer.Ordinal);
        var endpointCount = 0;

        foreach (var (tree, filePath) in syntaxTrees)
        {
            var root = tree.GetRoot();

            var allUsings = analysis.AllUsingsByFile.GetValueOrDefault(filePath) ?? _emptyUsings;
            var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(filePath) ?? _emptyAliases;

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.Modifiers.Any(m => m.Text == "file"))
                    continue;

                var baseTypes = GetBaseTypes(classDecl);
                var endpointBase = FindEndpointBaseType(baseTypes);

                if (endpointBase == null)
                    continue;

                endpointCount++;
                var className = GetFullTypeName(classDecl);
                Console.WriteLine($"  Found endpoint: {className}");

                var currentNs = GetContainingNamespace(classDecl);

                var typeArgs = IsFluentEndpointBaseType(endpointBase)
                                   ? ExtractFluentTypeArguments(endpointBase)
                                   : ExtractTypeArguments(endpointBase);

                foreach (var typeArg in typeArgs)
                    ProcessTypeExpression(typeArg, currentNs, className, allUsings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
            }
        }

        return (serializableTypes, endpointCount);
    }

    private static List<string> GetBaseTypes(ClassDeclarationSyntax classDecl)
        => classDecl.BaseList == null
               ? []
               : classDecl.BaseList.Types.Select(t => t.ToString()).ToList();

    private static string? FindEndpointBaseType(List<string> baseTypes)
    {
        foreach (var baseType in baseTypes)
        {
            if (IsFluentEndpointBaseType(baseType))
                return baseType;

            var baseSpan = baseType.AsSpan();
            var genIdx = baseSpan.IndexOf('<');
            if (genIdx >= 0)
                baseSpan = baseSpan[..genIdx];

            var dotIdx = baseSpan.LastIndexOf('.');
            var baseName = dotIdx >= 0 ? baseSpan[(dotIdx + 1)..].Trim() : baseSpan.Trim();

            foreach (var pattern in _config.EndpointBaseTypePatterns)
            {
                if (baseName.StartsWith(pattern.AsSpan(), StringComparison.Ordinal))
                    return baseType;
            }
        }

        return null;
    }

    private static bool IsFluentEndpointBaseType(string baseType)
    {
        var span = baseType.AsSpan();
        var genericIdx = span.IndexOf('<');

        if (genericIdx > 0)
            span = span[..genericIdx];

        span = span.Trim();

        return span.StartsWith("Ep.", StringComparison.Ordinal) ||
               span.Contains(".Ep.", StringComparison.Ordinal);
    }

    private static List<string> ExtractTypeArguments(string genericType)
    {
        var result = new List<string>();
        var match = TypeArgMatcherRegex().Match(genericType);

        if (match.Success)
        {
            var argsString = match.Groups[1].Value;
            var args = SplitTypeArguments(argsString);
            result.AddRange(args);
        }

        return result;
    }

    private static List<string> ExtractFluentTypeArguments(string fluentBaseType)
    {
        var result = new List<string>();

        foreach (var segment in SplitDotSegments(fluentBaseType))
        {
            var span = segment.AsSpan();
            var genericIdx = span.IndexOf('<');
            var segName = genericIdx > 0 ? span[..genericIdx].Trim() : span.Trim();

            if (!segName.Equals("Req", StringComparison.Ordinal) && !segName.Equals("Res", StringComparison.Ordinal))
                continue;

            result.AddRange(ExtractTypeArguments(segment));
        }

        return result;
    }

    private static List<string> SplitBalanced(string input, char delimiter, bool trimItems = false)
    {
        var result = new List<string>();
        var depth = 0;
        var lastSplit = 0;

        for (var i = 0; i < input.Length; i++)
        {
            switch (input[i])
            {
                case '<':
                    depth++;

                    break;
                case '>':
                    depth--;

                    break;
                default:
                    if (depth == 0 && input[i] == delimiter)
                    {
                        var item = trimItems ? input[lastSplit..i].Trim() : input[lastSplit..i];
                        if (item.Length > 0)
                            result.Add(item);
                        lastSplit = i + 1;
                    }

                    break;
            }
        }

        if (lastSplit < input.Length)
        {
            var item = trimItems ? input[lastSplit..].Trim() : input[lastSplit..];
            if (item.Length > 0)
                result.Add(item);
        }

        return result;
    }

    private static List<string> SplitDotSegments(string typeName)
        => SplitBalanced(typeName, '.');

    private static List<string> SplitTypeArguments(string argsString)
        => SplitBalanced(argsString, ',', true);

    [GeneratedRegex("<(.+)>")]
    private static partial Regex TypeArgMatcherRegex();
}