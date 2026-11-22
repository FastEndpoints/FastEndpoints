using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static FastEndpoints.Generator.Cli.TypeNames;

namespace FastEndpoints.Generator.Cli;

sealed class NuGetPackageTypeLoader
{
    private static readonly SymbolDisplayFormat _qualifiedTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandNullable);

    private static readonly SymbolDisplayFormat _namespaceFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private readonly Dictionary<string, INamedTypeSymbol> _typesByFullName;
    private readonly Dictionary<string, string> _typeNamespaces;
    private readonly Dictionary<string, List<string>> _typesBySimpleName;
    private readonly Dictionary<string, TypeInfo> _typeInfoCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _resolutionCache = new(StringComparer.Ordinal);

    private NuGetPackageTypeLoader(Dictionary<string, INamedTypeSymbol> typesByFullName,
                                   Dictionary<string, string> typeNamespaces,
                                   Dictionary<string, List<string>> typesBySimpleName)
    {
        _typesByFullName = typesByFullName;
        _typeNamespaces = typeNamespaces;
        _typesBySimpleName = typesBySimpleName;
    }

    public static NuGetPackageTypeLoader Create(List<NuGetPackageAssemblyInfo> packageAssemblies, List<string> referenceAssemblies)
    {
        var typesByFullName = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var typeNamespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        var typesBySimpleName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (packageAssemblies.Count == 0)
            return new(typesByFullName, typeNamespaces, typesBySimpleName);

        var packageAssemblyLookup = packageAssemblies.ToDictionary(a => a.AssemblyPath, a => a.LibraryKey, StringComparer.OrdinalIgnoreCase);
        var metadataReferencePaths = referenceAssemblies.Concat(packageAssemblies.Select(a => a.AssemblyPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var metadataReferences = CreateMetadataReferences(metadataReferencePaths, packageAssemblyLookup);

        if (metadataReferences.Count == 0)
            return new(typesByFullName, typeNamespaces, typesBySimpleName);

        var compilation = CSharpCompilation.Create("FastEndpoints.Generator.Cli.NuGetMetadata", references: metadataReferences.Values);

        foreach (var assemblyPath in packageAssemblies.Select(a => a.AssemblyPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!metadataReferences.TryGetValue(assemblyPath, out var reference))
                continue;

            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                continue;

            CollectTypes(assemblySymbol.GlobalNamespace, typesByFullName, typeNamespaces, typesBySimpleName);
        }

        return new(typesByFullName, typeNamespaces, typesBySimpleName);
    }

    private static Dictionary<string, MetadataReference> CreateMetadataReferences(IEnumerable<string> metadataReferencePaths, Dictionary<string, string> packageAssemblyLookup)
    {
        var referencesByPath = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in metadataReferencePaths)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Diagnostics.Report(CreateMetadataWarning(path, packageAssemblyLookup, "missing metadata reference"));

                    continue;
                }

                referencesByPath[path] = MetadataReference.CreateFromFile(path);
            }
            catch (Exception ex)
            {
                Diagnostics.Report(CreateMetadataWarning(path, packageAssemblyLookup, ex.Message));
            }
        }

        return referencesByPath;
    }

    private static string CreateMetadataWarning(string path, Dictionary<string, string> packageAssemblyLookup, string reason)
        => packageAssemblyLookup.TryGetValue(path, out var libraryKey)
               ? $"Warning: skipping metadata reference '{path}' from package '{libraryKey}': {reason}"
               : $"Warning: skipping metadata reference '{path}': {reason}";

    public bool TryResolveAndLoadType(string typeExpression, string currentNamespace, List<string> usings, out string fullName, out TypeInfo typeInfo)
    {
        fullName = string.Empty;
        typeInfo = null!;

        var resolvedFullName = ResolveFullName(typeExpression, currentNamespace, usings);

        if (resolvedFullName == null || !_typesByFullName.TryGetValue(resolvedFullName, out var symbol))
            return false;

        fullName = resolvedFullName;

        if (!_typeInfoCache.TryGetValue(fullName, out var cachedTypeInfo))
        {
            cachedTypeInfo = CreateTypeInfo(symbol);
            _typeInfoCache[fullName] = cachedTypeInfo;
        }

        typeInfo = cachedTypeInfo;

        return true;
    }

    private string? ResolveFullName(string typeExpression, string currentNamespace, List<string> usings)
    {
        var lookupName = NormalizeLookupName(typeExpression);
        var cacheKey = BuildResolutionKey(lookupName, currentNamespace, usings);

        if (_resolutionCache.TryGetValue(cacheKey, out var cachedFullName))
            return cachedFullName;

        var resolvedFullName = ResolveFullNameCore(lookupName, currentNamespace, usings);
        _resolutionCache[cacheKey] = resolvedFullName;

        return resolvedFullName;
    }

    private string? ResolveFullNameCore(string lookupName, string currentNamespace, List<string> usings)
    {
        if (string.IsNullOrWhiteSpace(lookupName))
            return null;

        if (_typesByFullName.ContainsKey(lookupName))
            return lookupName;

        if (!string.IsNullOrWhiteSpace(currentNamespace))
        {
            var currentNamespaceCandidate = $"{currentNamespace}.{lookupName}";

            if (_typesByFullName.ContainsKey(currentNamespaceCandidate))
                return currentNamespaceCandidate;
        }

        foreach (var usingNamespace in usings)
        {
            var usingCandidate = $"{usingNamespace}.{lookupName}";

            if (_typesByFullName.ContainsKey(usingCandidate))
                return usingCandidate;
        }

        var simpleName = SimpleName(lookupName);

        if (!_typesBySimpleName.TryGetValue(simpleName, out var candidates) || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var contextualCandidates = candidates.Where(c => MatchesContext(c, currentNamespace, usings)).ToList();

        if (contextualCandidates.Count == 1)
            return contextualCandidates[0];

        var ambiguousCandidates = contextualCandidates.Count > 1 ? contextualCandidates : candidates;
        Diagnostics.Report(
            $"Warning: ambiguous external type reference '{lookupName}' matched {string.Join(", ", ambiguousCandidates.OrderBy(x => x, StringComparer.Ordinal))}. Use a fully qualified type name or alias.");

        return null;
    }

    private bool MatchesContext(string fullName, string currentNamespace, List<string> usings)
    {
        if (!_typeNamespaces.TryGetValue(fullName, out var typeNamespace))
            return false;

        return string.Equals(typeNamespace, currentNamespace, StringComparison.Ordinal) ||
               usings.Contains(typeNamespace, StringComparer.Ordinal);
    }

    private static string BuildResolutionKey(string lookupName, string currentNamespace, List<string> usings)
    {
        var sb = new System.Text.StringBuilder(lookupName.Length + currentNamespace.Length + usings.Count * 32);
        sb.Append(lookupName);
        sb.Append('|');
        sb.Append(currentNamespace);
        sb.Append('|');

        for (var i = 0; i < usings.Count; i++)
        {
            if (i > 0)
                sb.Append(';');

            sb.Append(usings[i]);
        }

        return sb.ToString();
    }

    private static string NormalizeLookupName(string typeExpression)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return string.Empty;

        var normalizedType = StripGlobal(typeExpression.Trim().TrimEnd('?'));

        var genericIndex = normalizedType.IndexOf('<');

        if (genericIndex >= 0)
            normalizedType = normalizedType[..genericIndex];

        var arrayIndex = normalizedType.IndexOf('[');

        if (arrayIndex >= 0)
            normalizedType = normalizedType[..arrayIndex];

        return normalizedType.Trim();
    }

    private static void CollectTypes(INamespaceSymbol namespaceSymbol,
                                     Dictionary<string, INamedTypeSymbol> typesByFullName,
                                     Dictionary<string, string> typeNamespaces,
                                     Dictionary<string, List<string>> typesBySimpleName)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
            CollectTypes(typeSymbol, typesByFullName, typeNamespaces, typesBySimpleName);

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            CollectTypes(childNamespace, typesByFullName, typeNamespaces, typesBySimpleName);
    }

    private static void CollectTypes(INamedTypeSymbol typeSymbol,
                                     Dictionary<string, INamedTypeSymbol> typesByFullName,
                                     Dictionary<string, string> typeNamespaces,
                                     Dictionary<string, List<string>> typesBySimpleName)
    {
        if (IsPubliclyAccessible(typeSymbol))
        {
            var fullName = GetTypeDefinitionName(typeSymbol);

            if (!typesByFullName.ContainsKey(fullName))
            {
                typesByFullName[fullName] = typeSymbol;
                typeNamespaces[fullName] = GetNamespace(typeSymbol);

                //keyed like the lookups (Name`arity for generics), so Foo and Foo<T> don't collide
                var simpleName = typeSymbol.Arity > 0 ? $"{typeSymbol.Name}`{typeSymbol.Arity}" : typeSymbol.Name;

                if (!typesBySimpleName.TryGetValue(simpleName, out var matches))
                {
                    matches = [];
                    typesBySimpleName[simpleName] = matches;
                }

                matches.Add(fullName);
            }
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
            CollectTypes(nestedType, typesByFullName, typeNamespaces, typesBySimpleName);
    }

    private static bool IsPubliclyAccessible(INamedTypeSymbol typeSymbol)
    {
        if (!typeSymbol.CanBeReferencedByName || typeSymbol.DeclaredAccessibility != Accessibility.Public)
            return false;

        for (var containingType = typeSymbol.ContainingType; containingType != null; containingType = containingType.ContainingType)
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
                return false;
        }

        return true;
    }

    private static TypeInfo CreateTypeInfo(INamedTypeSymbol typeSymbol)
    {
        typeSymbol = typeSymbol.OriginalDefinition;

        var fullName = GetTypeDefinitionName(typeSymbol);
        var filePath = fullName;
        var properties = typeSymbol.GetMembers()
                                   .OfType<IPropertySymbol>()
                                   .Where(p => p is { IsStatic: false, IsIndexer: false } && IsSerializableProperty(p))
                                   .Select(p => new PropertyInfo(p.Name, GetDisplayTypeName(p.Type), filePath))
                                   .ToList();

        var baseTypes = new List<TypeRef>();

        if (typeSymbol is { TypeKind: TypeKind.Class, BaseType: { SpecialType: not SpecialType.System_Object } baseType })
            baseTypes.Add(new(GetDisplayTypeName(baseType), filePath));

        var genericParameters = typeSymbol.TypeParameters.Select(p => p.Name).ToList();

        return new(fullName, GetNamespace(typeSymbol), typeSymbol.Name, filePath, properties, baseTypes, genericParameters);
    }

    private static bool IsSerializableProperty(IPropertySymbol propertySymbol)
        => propertySymbol.DeclaredAccessibility == Accessibility.Public ||
           propertySymbol.GetMethod?.DeclaredAccessibility == Accessibility.Public ||
           propertySymbol.SetMethod?.DeclaredAccessibility == Accessibility.Public;

    private static string GetNamespace(INamedTypeSymbol typeSymbol)
        => typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.ToDisplayString(_namespaceFormat);

    private static string GetTypeDefinitionName(INamedTypeSymbol typeSymbol)
    {
        var nameParts = new Stack<string>();

        for (var current = typeSymbol.OriginalDefinition; current != null; current = current.ContainingType)
        {
            var name = current.Name;

            if (current.Arity > 0)
                name = $"{name}`{current.Arity}";

            nameParts.Push(name);
        }

        var namespaceName = GetNamespace(typeSymbol);
        var typeName = string.Join('.', nameParts);

        return string.IsNullOrWhiteSpace(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
    }

    private static string GetDisplayTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol switch
        {
            IArrayTypeSymbol arrayType => $"{GetDisplayTypeName(arrayType.ElementType)}[]",
            INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } namedType => GetDisplayTypeName(namedType.TypeArguments[0]),
            INamedTypeSymbol namedType => GetNamedTypeDisplayName(namedType),
            ITypeParameterSymbol typeParameter => typeParameter.Name,
            _ => typeSymbol.ToDisplayString(_qualifiedTypeFormat)
        };
    }

    private static string GetNamedTypeDisplayName(INamedTypeSymbol typeSymbol)
    {
        var rootTypeName = GetTypeDefinitionName(typeSymbol.OriginalDefinition);

        if (!typeSymbol.IsGenericType || typeSymbol.TypeArguments.Length == 0)
            return rootTypeName;

        var typeArguments = typeSymbol.TypeArguments.Select(GetDisplayTypeName);

        return $"{rootTypeName}<{string.Join(", ", typeArguments)}>";
    }
}