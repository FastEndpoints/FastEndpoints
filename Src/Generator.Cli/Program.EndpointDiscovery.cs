using static FastEndpoints.Generator.Cli.TypeNames;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static HashSet<string> DiscoverSerializableTypesFromEndpoints(List<EndpointDeclaration> endpoints, AnalysisContext analysis)
    {
        var serializableTypes = new HashSet<string>(StringComparer.Ordinal);
        string? currentFile = null;
        TypeScope fileScope = null!;

        foreach (var endpoint in endpoints)
        {
            Console.WriteLine($"  Found endpoint: {endpoint.ClassName}");

            //endpoints are grouped by file. resolve the file scope once per file, before any of its endpoints trigger external loads.
            if (!string.Equals(currentFile, endpoint.FilePath, StringComparison.Ordinal))
            {
                currentFile = endpoint.FilePath;
                fileScope = analysis.ScopeFor(currentFile, "", "");
            }

            var typeArgs = IsFluentEndpointBaseType(endpoint.BaseType)
                               ? ExtractFluentTypeArguments(endpoint.BaseType)
                               : ExtractTypeArguments(endpoint.BaseType);

            var scope = fileScope with { Namespace = endpoint.Namespace, TypeFullName = endpoint.ClassName };

            foreach (var typeArg in typeArgs)
                ProcessTypeExpression(typeArg, scope, serializableTypes, analysis);
        }

        return serializableTypes;
    }

    private static string? FindEndpointBaseType(List<TypeRef> baseTypes)
    {
        foreach (var (baseType, _) in baseTypes)
        {
            if (IsFluentEndpointBaseType(baseType))
                return baseType;

            var baseName = GetSimpleBaseTypeName(baseType);

            foreach (var pattern in _config.EndpointBaseTypePatterns)
            {
                if (baseName.Equals(pattern.AsSpan(), StringComparison.Ordinal))
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
}