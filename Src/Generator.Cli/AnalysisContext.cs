namespace FastEndpoints.Generator.Cli;

record AnalysisContext(Dictionary<string, TypeInfo> TypesByFullName,
                       Dictionary<string, List<string>> AllUsingsByFile,
                       Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile)
{
    private Func<ReferencedProjectData>? _referencedProjectLoader;
    private Func<NuGetPackageTypeLoader>? _nuGetPackageLoader;
    private NuGetPackageTypeLoader? _loadedNuGetPackageTypes;

    public Dictionary<string, string> SimpleNameToFullName { get; } = new(StringComparer.Ordinal);
    public HashSet<string> AmbiguousSimpleNames { get; } = new(StringComparer.Ordinal);
    public bool ReferencedProjectsInspected { get; private set; }
    public string? ReferencedProjectsHash { get; private set; }
    public bool NuGetPackagesInspected { get; private set; }
    public string? NuGetPackagesHash { get; private set; }

    public static AnalysisContext Create(Dictionary<string, TypeInfo> types, Dictionary<string, List<string>> usings, Dictionary<string, Dictionary<string, string>> aliases)
    {
        var ctx = new AnalysisContext(types, usings, aliases);

        foreach (var fullName in types.Keys)
            ctx.TrackSimpleName(fullName);

        return ctx;
    }

    public void SetReferencedProjectLoader(Func<ReferencedProjectData> referencedProjectLoader)
        => _referencedProjectLoader = referencedProjectLoader;

    public void SetNuGetPackageLoader(Func<NuGetPackageTypeLoader> nuGetPackageLoader)
        => _nuGetPackageLoader = nuGetPackageLoader;

    public void EnsureReferencedProjectsLoaded()
    {
        if (ReferencedProjectsInspected)
            return;

        ReferencedProjectsInspected = true;

        if (_referencedProjectLoader == null)
            return;

        var referencedProjectData = _referencedProjectLoader();
        ReferencedProjectsHash = referencedProjectData.Hash;

        foreach (var (fullName, typeInfo) in referencedProjectData.TypesByFullName)
        {
            if (TypesByFullName.TryGetValue(fullName, out var existingType))
                TypesByFullName[fullName] = Program.MergeTypeInfos(existingType, typeInfo);
            else
                TypesByFullName[fullName] = typeInfo;

            TrackSimpleName(fullName);
        }

        foreach (var (filePath, usings) in referencedProjectData.AllUsingsByFile)
            AllUsingsByFile[filePath] = usings;

        foreach (var (filePath, aliases) in referencedProjectData.AllTypeAliasesByFile)
            AllTypeAliasesByFile[filePath] = aliases;
    }

    public string? TryLoadNuGetType(string typeExpression, string currentNamespace, List<string> usings, int arity = 0)
    {
        var packageLoader = EnsureNuGetPackageLoader();

        if (packageLoader == null)
            return null;

        if (!packageLoader.TryResolveAndLoadType(typeExpression, currentNamespace, usings, out var fullName, out var typeInfo, arity))
            return null;

        MergeType(typeInfo);

        return fullName;
    }

    private NuGetPackageTypeLoader? EnsureNuGetPackageLoader()
    {
        if (_loadedNuGetPackageTypes != null)
            return _loadedNuGetPackageTypes;

        NuGetPackagesInspected = true;

        if (_nuGetPackageLoader == null)
            return null;

        _loadedNuGetPackageTypes = _nuGetPackageLoader();
        NuGetPackagesHash = _loadedNuGetPackageTypes.Hash;

        return _loadedNuGetPackageTypes;
    }

    private void MergeType(TypeInfo typeInfo)
    {
        if (TypesByFullName.TryGetValue(typeInfo.FullName, out var existingType))
            TypesByFullName[typeInfo.FullName] = Program.MergeTypeInfos(existingType, typeInfo);
        else
            TypesByFullName[typeInfo.FullName] = typeInfo;

        TrackSimpleName(typeInfo.FullName);
    }

    private void TrackSimpleName(string fullName)
    {
        var dotIndex = fullName.LastIndexOf('.');
        var simpleName = dotIndex >= 0 ? fullName[(dotIndex + 1)..] : fullName;

        if (AmbiguousSimpleNames.Contains(simpleName))
            return;

        if (!SimpleNameToFullName.ContainsKey(simpleName))
            SimpleNameToFullName[simpleName] = fullName;
        else if (!string.Equals(SimpleNameToFullName[simpleName], fullName, StringComparison.Ordinal))
        {
            SimpleNameToFullName.Remove(simpleName);
            AmbiguousSimpleNames.Add(simpleName);
        }
    }
}