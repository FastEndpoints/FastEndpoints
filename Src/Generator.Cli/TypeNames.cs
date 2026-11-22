namespace FastEndpoints.Generator.Cli;

/// <summary>
/// string-level helpers for the c# type name grammar used by the source analysis.
/// </summary>
static class TypeNames
{
    static readonly Dictionary<string, string> _rootTypeCache = new(StringComparer.Ordinal);

    internal static string StripGlobal(string typeName)
        => typeName.StartsWith("global::", StringComparison.Ordinal) ? typeName[8..] : typeName;

    internal static string ExpandAliasInTypeName(string typeName, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.StartsWith("global::", StringComparison.Ordinal))
            return typeName;

        var stop = typeName.IndexOfAny(['.', '<', '[']);

        if (stop < 0)
            stop = typeName.Length;

        return aliases.TryGetValue(typeName[..stop], out var target)
                   ? target + typeName[stop..]
                   : typeName;
    }

    internal static string SimpleName(string typeName)
    {
        var dotIndex = typeName.LastIndexOf('.');

        return dotIndex >= 0 ? typeName[(dotIndex + 1)..] : typeName;
    }

    internal static string GetSimpleNameWithoutArity(string rootTypeName)
    {
        var simpleTypeName = SimpleName(rootTypeName);
        var backtickIdx = simpleTypeName.IndexOf('`');

        return backtickIdx >= 0 ? simpleTypeName[..backtickIdx] : simpleTypeName;
    }

    internal static ReadOnlySpan<char> GetSimpleBaseTypeName(string baseType)
    {
        var baseSpan = baseType.AsSpan();
        var genIdx = baseSpan.IndexOf('<');
        if (genIdx >= 0)
            baseSpan = baseSpan[..genIdx];

        var dotIdx = baseSpan.LastIndexOf('.');

        return dotIdx >= 0 ? baseSpan[(dotIdx + 1)..].Trim() : baseSpan.Trim();
    }

    internal static string ExtractRootType(string typeName)
    {
        if (_rootTypeCache.TryGetValue(typeName, out var rootType))
            return rootType;

        var tn = typeName.TrimEnd('?');
        var genericIndex = tn.IndexOf('<');

        if (genericIndex > 0)
        {
            var arity = CountTopLevelGenericArgs(tn, genericIndex);
            tn = $"{tn[..genericIndex]}`{arity}";
        }

        var arrayIndex = tn.IndexOf('[');
        if (arrayIndex > 0)
            tn = tn[..arrayIndex];

        rootType = tn.Trim();
        _rootTypeCache[typeName] = rootType;

        return rootType;
    }

    internal static int CountTopLevelGenericArgs(string typeName, int genericIndex)
    {
        var depth = 0;
        var count = 1;

        for (var i = genericIndex + 1; i < typeName.Length; i++)
        {
            switch (typeName[i])
            {
                case '<':
                    depth++;

                    break;
                case '>':
                    if (depth == 0)
                        return count;

                    depth--;

                    break;
                case ',':
                    if (depth == 0)
                        count++;

                    break;
            }
        }

        return count;
    }

    internal static string StripArityFromTypeName(string typeName)
    {
        var backtickIndex = typeName.IndexOf('`');

        if (backtickIndex < 0)
            return typeName;

        var endIndex = backtickIndex + 1;

        while (endIndex < typeName.Length && char.IsDigit(typeName[endIndex]))
            endIndex++;

        return string.Concat(typeName.AsSpan(0, backtickIndex), typeName.AsSpan(endIndex));
    }

    internal static List<string> ExtractTypeArguments(string genericType)
    {
        var result = new List<string>();
        var depth = 0;
        var start = -1;

        for (var i = 0; i < genericType.Length; i++)
        {
            if (genericType[i] == '<')
            {
                if (depth == 0)
                    start = i + 1;

                depth++;
            }
            else if (genericType[i] == '>')
            {
                depth--;

                if (depth == 0 && start != -1)
                {
                    var argsString = genericType.Substring(start, i - start);
                    result.AddRange(SplitTypeArguments(argsString));
                    start = -1;
                }
            }
        }

        return result;
    }

    internal static List<string> SplitDotSegments(string typeName)
        => SplitBalanced(typeName, '.');

    static List<string> SplitTypeArguments(string argsString)
        => SplitBalanced(argsString, ',', true);

    static List<string> SplitBalanced(string input, char delimiter, bool trimItems = false)
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
}
