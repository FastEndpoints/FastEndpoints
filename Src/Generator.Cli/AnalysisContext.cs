using static FastEndpoints.Generator.Cli.TypeNames;

namespace FastEndpoints.Generator.Cli;

sealed class AnalysisContext
{
    private static readonly Dictionary<string, string> _emptyAliases = new(StringComparer.Ordinal);
    private static readonly List<string> _emptyUsings = [];

    private readonly SourceIndex _index;
    private readonly Func<SourceIndex> _referencedProjectLoader;
    private readonly Func<NuGetPackageTypeLoader> _nuGetPackageLoader;
    private Task<(NuGetPackageTypeLoader Loader, IEnumerable<string> Warnings)>? _nuGetPackageLoadTask;
    private NuGetPackageTypeLoader? _loadedNuGetPackageTypes;

    private readonly Dictionary<string, string> _simpleNameToFullName = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguousSimpleNames = new(StringComparer.Ordinal);

    public Dictionary<string, TypeInfo> TypesByFullName => _index.Types;
    public bool ReferencedProjectsInspected { get; private set; }
    public bool NuGetPackagesInspected { get; private set; }

    public AnalysisContext(SourceIndex index, Func<SourceIndex> referencedProjectLoader, Func<NuGetPackageTypeLoader> nuGetPackageLoader)
    {
        _index = index;
        _referencedProjectLoader = referencedProjectLoader;
        _nuGetPackageLoader = nuGetPackageLoader;

        foreach (var fullName in index.Types.Keys)
            TrackSimpleName(fullName);
    }

    public TypeScope ScopeFor(string filePath, string ns, string typeFullName)
        => new(
            ns,
            typeFullName,
            _index.UsingsByFile.GetValueOrDefault(filePath) ?? _emptyUsings,
            _index.AliasesByFile.GetValueOrDefault(filePath) ?? _emptyAliases);

    /// <summary>
    /// resolves a type name (generic roots use the <c>Name`arity</c> form) against the indexed sources. if that fails and
    /// <paramref name="allowExternalFallback" /> is set, referenced projects are loaded and the lookup is retried before trying nuget metadata.
    /// </summary>
    public string? ResolveType(string lookupName, TypeScope scope, bool allowExternalFallback)
    {
        var resolved = ResolveLocal(lookupName, scope);

        if (resolved != null || !allowExternalFallback)
            return resolved;

        EnsureReferencedProjectsLoaded();

        return ResolveLocal(lookupName, scope) ?? TryLoadNuGetType(lookupName, scope.Namespace, scope.Usings);
    }

    private string? ResolveLocal(string typeName, TypeScope scope)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        typeName = typeName.Trim();
        typeName = typeName.TrimEnd('?');
        typeName = StripGlobal(typeName);

        if (scope.Aliases.Count > 0)
            typeName = ExpandAliasInTypeName(typeName, scope.Aliases);

        var lookupName = ExtractRootType(typeName);
        var types = _index.Types;

        if (types.ContainsKey(typeName))
            return typeName;

        if (!string.Equals(lookupName, typeName, StringComparison.Ordinal) && types.ContainsKey(lookupName))
            return lookupName;

        if (!string.IsNullOrEmpty(scope.TypeFullName) && lookupName.IndexOf('.') < 0)
        {
            foreach (var candidate in GetNestedTypeCandidates(scope.TypeFullName, lookupName))
            {
                if (types.ContainsKey(candidate))
                    return candidate;
            }
        }

        if (!string.IsNullOrEmpty(scope.Namespace))
        {
            var fullName = $"{scope.Namespace}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        foreach (var usingNs in scope.Usings)
        {
            var fullName = $"{usingNs}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        if (_ambiguousSimpleNames.Contains(lookupName))
        {
            Diagnostics.Report($"Warning: ambiguous type reference '{lookupName}' in '{scope.TypeFullName}'. Use a fully qualified type name or alias to disambiguate.");

            return null;
        }

        return _simpleNameToFullName.GetValueOrDefault(lookupName);
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

    private void EnsureReferencedProjectsLoaded()
    {
        if (ReferencedProjectsInspected)
            return;

        ReferencedProjectsInspected = true;

        //the nuget loader doesn't depend on referenced projects. start it now so both loads overlap.
        //NuGetPackagesInspected and the loader's warnings only surface when the loader is actually consulted.
        _nuGetPackageLoadTask = Task.Run(() => Diagnostics.Capture(_nuGetPackageLoader));

        var referenced = _referencedProjectLoader();

        foreach (var typeInfo in referenced.Types.Values)
            MergeType(typeInfo);

        foreach (var (filePath, usings) in referenced.UsingsByFile)
            _index.UsingsByFile[filePath] = usings;

        foreach (var (filePath, aliases) in referenced.AliasesByFile)
            _index.AliasesByFile[filePath] = aliases;
    }

    private string? TryLoadNuGetType(string typeExpression, string currentNamespace, List<string> usings)
    {
        if (!EnsureNuGetPackageLoader().TryResolveAndLoadType(typeExpression, currentNamespace, usings, out var fullName, out var typeInfo))
            return null;

        MergeType(typeInfo);

        return fullName;
    }

    private NuGetPackageTypeLoader EnsureNuGetPackageLoader()
    {
        if (_loadedNuGetPackageTypes != null)
            return _loadedNuGetPackageTypes;

        NuGetPackagesInspected = true;

        //ResolveType always loads referenced projects first, which starts this task
        var (loader, warnings) = _nuGetPackageLoadTask!.GetAwaiter().GetResult();

        foreach (var warning in warnings)
            Diagnostics.Report(warning);

        return _loadedNuGetPackageTypes = loader;
    }

    private void MergeType(TypeInfo typeInfo)
    {
        TypeInfo.AddOrMerge(_index.Types, typeInfo.FullName, typeInfo);

        TrackSimpleName(typeInfo.FullName);
    }

    private void TrackSimpleName(string fullName)
    {
        var simpleName = SimpleName(fullName);

        if (_ambiguousSimpleNames.Contains(simpleName))
            return;

        if (!_simpleNameToFullName.TryAdd(simpleName, fullName) &&
            !string.Equals(_simpleNameToFullName[simpleName], fullName, StringComparison.Ordinal))
        {
            _simpleNameToFullName.Remove(simpleName);
            _ambiguousSimpleNames.Add(simpleName);
        }
    }
}