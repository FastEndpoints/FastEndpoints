using System.Text;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static readonly Dictionary<string, string> _rootTypeCache = new(StringComparer.Ordinal);

    private static readonly HashSet<string> _primitiveTypeNames = new(StringComparer.Ordinal)
    {
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "dynamic",
        "float",
        "int",
        "long",
        "nint",
        "nuint",
        "object",
        "sbyte",
        "short",
        "string",
        "uint",
        "ulong",
        "ushort"
    };

    private static string? ResolveTypeName(string typeName,
                                           string currentNamespace,
                                           string currentTypeFullName,
                                           List<string> usings,
                                           Dictionary<string, string> typeAliases,
                                           Dictionary<string, TypeInfo> types,
                                           Dictionary<string, string>? simpleNameCache = null,
                                           HashSet<string>? ambiguousSimpleNames = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        typeName = typeName.Trim();
        typeName = typeName.TrimEnd('?');

        if (typeName.StartsWith("global::", StringComparison.Ordinal))
            typeName = typeName[8..];

        if (typeAliases.Count > 0)
            typeName = ExpandAliasInTypeName(typeName, typeAliases);

        var lookupName = ExtractRootType(typeName);

        if (types.ContainsKey(typeName))
            return typeName;

        if (!string.Equals(lookupName, typeName, StringComparison.Ordinal) && types.ContainsKey(lookupName))
            return lookupName;

        if (!string.IsNullOrEmpty(currentTypeFullName) && lookupName.IndexOf('.') < 0)
        {
            foreach (var candidate in GetNestedTypeCandidates(currentTypeFullName, lookupName))
            {
                if (types.ContainsKey(candidate))
                    return candidate;
            }
        }

        if (!string.IsNullOrEmpty(currentNamespace))
        {
            var fullName = $"{currentNamespace}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        foreach (var usingNs in usings)
        {
            var fullName = $"{usingNs}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        if (ambiguousSimpleNames != null && ambiguousSimpleNames.Contains(lookupName))
        {
            ReportDiagnostic($"Warning: ambiguous type reference '{lookupName}' in '{currentTypeFullName}'. Use a fully qualified type name or alias to disambiguate.");

            return null;
        }

        if (simpleNameCache != null && simpleNameCache.TryGetValue(lookupName, out var cached))
            return cached;

        return null;
    }

    private static IEnumerable<string> GetNestedTypeCandidates(string currentTypeFullName, string lookupName)
    {
        var prefix = currentTypeFullName;

        while (prefix.Length > 0)
        {
            yield return $"{prefix}.{lookupName}";

            var lastDot = prefix.LastIndexOf('.');

            if (lastDot < 0)
                break;

            prefix = prefix[..lastDot];
        }
    }

    private static bool ShouldSkipType(string fullTypeName, Dictionary<string, TypeInfo> allTypes)
    {
        var rootTypeName = ExtractRootType(fullTypeName);

        foreach (var skipNs in _config.SkipNamespaces)
        {
            if (rootTypeName.StartsWith(skipNs + ".", StringComparison.Ordinal) ||
                rootTypeName.StartsWith("global::" + skipNs + ".", StringComparison.Ordinal))
                return true;
        }

        var dotIndex = rootTypeName.LastIndexOf('.');
        var simpleTypeName = dotIndex >= 0 ? rootTypeName[(dotIndex + 1)..] : rootTypeName;
        var backtickIdx = simpleTypeName.IndexOf('`');

        if (backtickIdx >= 0)
            simpleTypeName = simpleTypeName[..backtickIdx];

        if (_config.SkipTypes.Contains(simpleTypeName))
            return true;

        if (allTypes.TryGetValue(rootTypeName, out var typeInfo))
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var baseTypeSpan = baseType.TypeName.AsSpan();
                var genIdx = baseTypeSpan.IndexOf('<');
                if (genIdx >= 0)
                    baseTypeSpan = baseTypeSpan[..genIdx];

                var baseDotIdx = baseTypeSpan.LastIndexOf('.');
                var baseTypeName = baseDotIdx >= 0 ? baseTypeSpan[(baseDotIdx + 1)..].Trim() : baseTypeSpan.Trim();

                foreach (var excluded in _config.ExcludedBaseTypes)
                {
                    if (baseTypeName.StartsWith(excluded.AsSpan(), StringComparison.Ordinal))
                        return true;
                }
            }
        }

        return false;
    }

    private static void AddTypeAndDependencies(string typeName, HashSet<string> serializableTypes, AnalysisContext analysis)
    {
        if (ShouldSkipType(typeName, analysis.TypesByFullName))
            return;

        var rootTypeName = ExtractRootType(typeName);

        if (!analysis.TypesByFullName.TryGetValue(rootTypeName, out var typeInfo))
            return;

        var currentNs = typeInfo.Namespace;
        var genericParameterMap = BuildGenericParameterMap(typeName, typeInfo);

        if (typeInfo.GenericParameters.Count > 0 && genericParameterMap.Count != typeInfo.GenericParameters.Count)
            return;

        if (!serializableTypes.Add(typeName))
            return;

        var dotIndex = rootTypeName.LastIndexOf('.');
        var simpleTypeName = dotIndex >= 0 ? rootTypeName[(dotIndex + 1)..] : rootTypeName;
        var backtickIdx = simpleTypeName.IndexOf('`');

        if (backtickIdx >= 0)
            simpleTypeName = simpleTypeName[..backtickIdx];

        var skipBaseTraversal = _config.RootTypeSkipNames.Contains(simpleTypeName);

        if (!skipBaseTraversal)
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var usings = analysis.AllUsingsByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyUsings;
                var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyAliases;
                var closedBaseType = ApplyGenericParameterMap(baseType.TypeName, genericParameterMap);
                ProcessTypeExpression(closedBaseType, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
            }
        }

        foreach (var prop in typeInfo.Properties)
        {
            var usings = analysis.AllUsingsByFile.GetValueOrDefault(prop.FilePath) ?? _emptyUsings;
            var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(prop.FilePath) ?? _emptyAliases;
            var closedPropertyType = ApplyGenericParameterMap(prop.TypeName, genericParameterMap);
            ProcessTypeExpression(closedPropertyType, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
        }
    }

    private static Dictionary<string, string> BuildGenericParameterMap(string closedTypeName, TypeInfo typeInfo)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (typeInfo.GenericParameters.Count == 0)
            return map;

        var typeArguments = ExtractTypeArguments(closedTypeName);

        if (typeArguments.Count != typeInfo.GenericParameters.Count)
            return map;

        for (var i = 0; i < typeInfo.GenericParameters.Count; i++)
            map[typeInfo.GenericParameters[i]] = typeArguments[i];

        return map;
    }

    private static string ApplyGenericParameterMap(string typeExpression, Dictionary<string, string> genericParameterMap, HashSet<string>? visited = null)
    {
        if (genericParameterMap.Count == 0 || string.IsNullOrWhiteSpace(typeExpression))
            return typeExpression;

        var sb = new StringBuilder(typeExpression.Length);

        for (var i = 0; i < typeExpression.Length;)
        {
            if (char.IsLetter(typeExpression[i]) || typeExpression[i] == '_')
            {
                var start = i++;

                while (i < typeExpression.Length && (char.IsLetterOrDigit(typeExpression[i]) || typeExpression[i] == '_'))
                    i++;

                var token = typeExpression[start..i];
                var prev = start > 0 ? typeExpression[start - 1] : '\0';
                var next = i < typeExpression.Length ? typeExpression[i] : '\0';
                var isQualifiedToken = prev == '.' || next == '.';

                if (!isQualifiedToken && genericParameterMap.TryGetValue(token, out var replacement))
                {
                    visited ??= new(StringComparer.Ordinal);

                    if (visited.Add(token))
                    {
                        sb.Append(ApplyGenericParameterMap(replacement, genericParameterMap, visited));
                        visited.Remove(token);
                    }
                    else
                        sb.Append(token);
                }
                else
                    sb.Append(token);

                continue;
            }

            sb.Append(typeExpression[i]);
            i++;
        }

        return sb.ToString();
    }

    private static void ProcessTypeExpression(string typeExpression,
                                              string currentNamespace,
                                              string currentTypeFullName,
                                              List<string> usings,
                                              Dictionary<string, string> typeAliases,
                                              HashSet<string> serializableTypes,
                                              AnalysisContext analysis,
                                              bool allowExternalFallback = false)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return;

        var fullResolvedType = ResolveFullTypeExpression(typeExpression, currentNamespace, currentTypeFullName, usings, typeAliases, analysis, allowExternalFallback);

        if (fullResolvedType != null)
        {
            var baseTypeWithoutArray = fullResolvedType;
            while (baseTypeWithoutArray.EndsWith("[]", StringComparison.Ordinal))
                baseTypeWithoutArray = baseTypeWithoutArray[..^2];

            var isBuiltInCollection = false;
            var genericIdx = baseTypeWithoutArray.IndexOf('<');

            if (genericIdx > 0)
            {
                var root = baseTypeWithoutArray[..genericIdx];
                var dotIdx = root.LastIndexOf('.');
                var simpleName = dotIdx >= 0 ? root[(dotIdx + 1)..] : root;

                if (_config.BuiltInCollectionTypes.Contains(simpleName))
                {
                    isBuiltInCollection = true;
                    serializableTypes.Add(baseTypeWithoutArray);
                }
            }

            if (!isBuiltInCollection)
                AddTypeAndDependencies(baseTypeWithoutArray, serializableTypes, analysis);

            if (fullResolvedType != baseTypeWithoutArray)
                serializableTypes.Add(fullResolvedType);

            const string ienumerablePrefix = "System.Collections.Generic.IEnumerable<";

            if (fullResolvedType.StartsWith(ienumerablePrefix, StringComparison.Ordinal))
            {
                var listType = string.Concat("System.Collections.Generic.List<", fullResolvedType.AsSpan(ienumerablePrefix.Length));
                serializableTypes.Add(listType);
            }
        }

        foreach (var arg in ExtractTypeArguments(typeExpression))
            ProcessTypeExpression(arg, currentNamespace, currentTypeFullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback);
    }

    private static string? ResolveFullTypeExpression(string typeExpression,
                                                     string currentNamespace,
                                                     string currentTypeFullName,
                                                     List<string> usings,
                                                     Dictionary<string, string> typeAliases,
                                                     AnalysisContext analysis,
                                                     bool allowExternalFallback = false)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return null;

        typeExpression = typeExpression.Trim();

        if (typeAliases.Count > 0)
            typeExpression = ExpandAliasInTypeName(typeExpression, typeAliases);

        if (typeExpression.EndsWith("?", StringComparison.Ordinal))
            typeExpression = typeExpression.TrimEnd('?');

        var arraySuffix = "";

        while (typeExpression.EndsWith("[]", StringComparison.Ordinal))
        {
            arraySuffix += "[]";
            typeExpression = typeExpression[..^2].TrimEnd();
        }

        var genericIndex = typeExpression.IndexOf('<');

        if (genericIndex > 0)
        {
            var rootName = typeExpression[..genericIndex];
            var arity = CountTopLevelGenericArgs(typeExpression, genericIndex);
            var arityRootName = $"{rootName}`{arity}";
            var resolvedRoot = ResolveTypeName(
                arityRootName,
                currentNamespace,
                currentTypeFullName,
                usings,
                typeAliases,
                analysis.TypesByFullName,
                analysis.SimpleNameToFullName,
                analysis.AmbiguousSimpleNames);

            if (resolvedRoot == null && allowExternalFallback && IsExternalFallbackCandidate(rootName))
            {
                analysis.EnsureReferencedProjectsLoaded();
                resolvedRoot = ResolveTypeName(
                                   arityRootName,
                                   currentNamespace,
                                   currentTypeFullName,
                                   usings,
                                   typeAliases,
                                   analysis.TypesByFullName,
                                   analysis.SimpleNameToFullName,
                                   analysis.AmbiguousSimpleNames) ??
                               analysis.TryLoadNuGetType(rootName, currentNamespace, usings, arity);
            }

            if (resolvedRoot == null)
            {
                var dotIndex = rootName.LastIndexOf('.');
                var simpleTypeName = dotIndex >= 0 ? rootName[(dotIndex + 1)..] : rootName;

                if (_config.BuiltInCollectionTypes.Contains(simpleTypeName))
                {
                    var collectionNamespace = rootName.Contains('.')
                                                  ? rootName[..rootName.LastIndexOf('.')]
                                                  : "System.Collections.Generic";
                    resolvedRoot = $"{collectionNamespace}.{simpleTypeName}";
                }
            }

            if (resolvedRoot == null)
                return null;

            resolvedRoot = StripArityFromTypeName(resolvedRoot);

            var args = ExtractTypeArguments(typeExpression);
            var resolvedArgs = new List<string>();

            foreach (var arg in args)
            {
                var resolvedArg = ResolveFullTypeExpression(arg, currentNamespace, currentTypeFullName, usings, typeAliases, analysis, allowExternalFallback);
                resolvedArgs.Add(resolvedArg ?? arg);
            }

            return $"{resolvedRoot}<{string.Join(", ", resolvedArgs)}>{arraySuffix}";
        }

        var resolved = ResolveTypeName(
            typeExpression,
            currentNamespace,
            currentTypeFullName,
            usings,
            typeAliases,
            analysis.TypesByFullName,
            analysis.SimpleNameToFullName,
            analysis.AmbiguousSimpleNames);

        if (resolved == null && allowExternalFallback && IsExternalFallbackCandidate(typeExpression))
        {
            analysis.EnsureReferencedProjectsLoaded();
            resolved = ResolveTypeName(
                           typeExpression,
                           currentNamespace,
                           currentTypeFullName,
                           usings,
                           typeAliases,
                           analysis.TypesByFullName,
                           analysis.SimpleNameToFullName,
                           analysis.AmbiguousSimpleNames) ??
                       analysis.TryLoadNuGetType(typeExpression, currentNamespace, usings);
        }

        return resolved != null ? resolved + arraySuffix : null;
    }

    private static bool IsExternalFallbackCandidate(string typeExpression)
    {
        var rootType = ExtractRootType(typeExpression);

        if (rootType.StartsWith("global::", StringComparison.Ordinal))
            rootType = rootType[8..];

        var strippedRoot = StripArityFromTypeName(rootType);

        foreach (var skipNs in _config.SkipNamespaces)
        {
            if (strippedRoot.StartsWith(skipNs + ".", StringComparison.Ordinal))
                return false;
        }

        var dotIndex = strippedRoot.LastIndexOf('.');
        var simpleName = dotIndex >= 0 && dotIndex < strippedRoot.Length - 1
                             ? strippedRoot[(dotIndex + 1)..]
                             : strippedRoot;

        if (string.IsNullOrWhiteSpace(simpleName))
            return false;

        if (_config.BuiltInCollectionTypes.Contains(simpleName) || _primitiveTypeNames.Contains(simpleName) || _primitiveTypeNames.Contains(strippedRoot))
            return false;

        return char.IsUpper(simpleName[0]);
    }

    private static string ExtractRootType(string typeName)
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

    private static int CountTopLevelGenericArgs(string typeName, int genericIndex)
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

    private static string StripArityFromTypeName(string typeName)
    {
        var backtickIndex = typeName.IndexOf('`');

        if (backtickIndex < 0)
            return typeName;

        var endIndex = backtickIndex + 1;

        while (endIndex < typeName.Length && char.IsDigit(typeName[endIndex]))
            endIndex++;

        return string.Concat(typeName.AsSpan(0, backtickIndex), typeName.AsSpan(endIndex));
    }

    private static string ExpandAliasInTypeName(string typeName, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.StartsWith("global::", StringComparison.Ordinal))
            return typeName;

        var stop = typeName.Length;
        var dot = typeName.IndexOf('.');

        if (dot >= 0)
            stop = Math.Min(stop, dot);

        var lt = typeName.IndexOf('<');

        if (lt >= 0)
            stop = Math.Min(stop, lt);

        var br = typeName.IndexOf('[');

        if (br >= 0)
            stop = Math.Min(stop, br);

        var root = stop == typeName.Length ? typeName : typeName[..stop];

        if (!aliases.TryGetValue(root, out var target))
            return typeName;

        var rest = stop == typeName.Length
                       ? string.Empty
                       : typeName[stop..];

        return target + rest;
    }
}