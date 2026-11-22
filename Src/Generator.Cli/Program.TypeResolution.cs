using System.Text;
using static FastEndpoints.Generator.Cli.TypeNames;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
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

    private static bool ShouldSkipType(string fullTypeName, Dictionary<string, TypeInfo> allTypes)
    {
        var rootTypeName = ExtractRootType(fullTypeName);

        if (_config.IsSkippedNamespace(rootTypeName) || _config.IsSkippedTypeName(GetSimpleNameWithoutArity(rootTypeName)))
            return true;

        if (allTypes.TryGetValue(rootTypeName, out var typeInfo))
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var baseTypeName = GetSimpleBaseTypeName(baseType.TypeName);

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

        var skipBaseTraversal = _config.RootTypeSkipNames.Contains(GetSimpleNameWithoutArity(rootTypeName));

        if (!skipBaseTraversal)
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var scope = analysis.ScopeFor(baseType.FilePath, currentNs, typeInfo.FullName);
                var closedBaseType = ApplyGenericParameterMap(baseType.TypeName, genericParameterMap);
                ProcessTypeExpression(closedBaseType, scope, serializableTypes, analysis);
            }
        }

        foreach (var prop in typeInfo.Properties)
        {
            var scope = analysis.ScopeFor(prop.FilePath, currentNs, typeInfo.FullName);
            var closedPropertyType = ApplyGenericParameterMap(prop.TypeName, genericParameterMap);
            ProcessTypeExpression(closedPropertyType, scope, serializableTypes, analysis);
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

    private static void ProcessTypeExpression(string typeExpression, TypeScope scope, HashSet<string> serializableTypes, AnalysisContext analysis)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return;

        var fullResolvedType = ResolveFullTypeExpression(typeExpression, scope, analysis);

        if (fullResolvedType != null)
        {
            var baseTypeWithoutArray = fullResolvedType;
            while (baseTypeWithoutArray.EndsWith("[]", StringComparison.Ordinal))
                baseTypeWithoutArray = baseTypeWithoutArray[..^2];

            var isBuiltInCollection = false;
            var genericIdx = baseTypeWithoutArray.IndexOf('<');

            if (genericIdx > 0)
            {
                var simpleName = SimpleName(baseTypeWithoutArray[..genericIdx]);

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
            ProcessTypeExpression(arg, scope, serializableTypes, analysis);
    }

    private static string? ResolveFullTypeExpression(string typeExpression, TypeScope scope, AnalysisContext analysis)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return null;

        typeExpression = typeExpression.Trim();

        if (scope.Aliases.Count > 0)
            typeExpression = ExpandAliasInTypeName(typeExpression, scope.Aliases);

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
            var rootName = typeExpression[..genericIndex].TrimEnd();
            //nuget display names already carry arity (Name`1<T>). strip it so it isn't appended twice.
            var arityRootName = $"{StripArityFromTypeName(rootName)}`{CountTopLevelGenericArgs(typeExpression, genericIndex)}";
            var resolvedRoot = analysis.ResolveType(arityRootName, scope, IsExternalFallbackCandidate(arityRootName));

            if (resolvedRoot == null)
            {
                var simpleTypeName = GetSimpleNameWithoutArity(rootName);

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
                var resolvedArg = ResolveFullTypeExpression(arg, scope, analysis);
                resolvedArgs.Add(resolvedArg ?? arg);
            }

            return $"{resolvedRoot}<{string.Join(", ", resolvedArgs)}>{arraySuffix}";
        }

        var resolved = analysis.ResolveType(typeExpression, scope, IsExternalFallbackCandidate(typeExpression));

        return resolved != null ? resolved + arraySuffix : null;
    }

    private static bool IsExternalFallbackCandidate(string typeExpression)
    {
        var rootType = StripGlobal(ExtractRootType(typeExpression));

        if (_config.IsSkippedNamespace(rootType))
            return false;

        var strippedRoot = StripArityFromTypeName(rootType);
        var simpleName = GetSimpleNameWithoutArity(rootType);

        if (string.IsNullOrWhiteSpace(simpleName))
            return false;

        if (_primitiveTypeNames.Contains(simpleName) || _primitiveTypeNames.Contains(strippedRoot) || _config.IsSkippedTypeName(simpleName))
            return false;

        //the built-in collections are generic. a non-generic type with the same name (ex: Queue) may be a user dto in another project.
        if (_config.BuiltInCollectionTypes.Contains(simpleName) && rootType.Contains('`'))
            return false;

        //only an unqualified name can mean the well-known type. a qualified one (or an expanded alias) points at a same-named user type.
        if (_config.WellKnownExternalTypes.Contains(simpleName) && !strippedRoot.Contains('.'))
            return false;

        return char.IsUpper(simpleName[0]);
    }
}