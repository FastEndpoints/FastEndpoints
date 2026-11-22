// ReSharper disable NotAccessedPositionalProperty.Global

namespace FastEndpoints.Generator.Cli;

record SourceFileSet(List<string> Files, string Hash);

record NuGetPackageAssemblySet(List<NuGetPackageAssemblyInfo> PackageAssemblies, List<string> ReferenceAssemblies, string Hash);

record NuGetPackageAssemblyInfo(string LibraryKey, string AssemblyPath);

readonly record struct TargetFrameworkMoniker(string Identifier, Version Version)
{
    public string BaseMoniker => $"{Identifier}{Version.Major}.{Version.Minor}";

    public bool IsCompatibleReferenceTarget(TargetFrameworkMoniker candidate)
        => string.Equals(Identifier, candidate.Identifier, StringComparison.OrdinalIgnoreCase) &&
           candidate.Version <= Version;

    //parses the framework part of a tfm such as "net10.0", "net10.0-windows" or "netstandard21". the platform suffix is ignored.
    public static bool TryParse(string? targetFramework, out TargetFrameworkMoniker moniker)
    {
        moniker = new(string.Empty, new(0, 0));

        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        var tfm = targetFramework.Trim();
        var separatorIndex = tfm.IndexOf('-');

        if (separatorIndex >= 0)
            tfm = tfm[..separatorIndex];

        if (!TryExtractIdentifierAndVersion(tfm, out var identifier, out var versionText))
            return false;

        if (!TryParseVersion(versionText, out var version))
            return false;

        moniker = new(identifier, version);

        return true;
    }

    static bool TryExtractIdentifierAndVersion(string targetFramework, out string identifier, out string versionText)
    {
        identifier = string.Empty;
        versionText = string.Empty;

        var index = 0;

        while (index < targetFramework.Length && !char.IsDigit(targetFramework[index]))
            index++;

        if (index == 0 || index >= targetFramework.Length)
            return false;

        identifier = targetFramework[..index];
        versionText = targetFramework[index..];

        return true;
    }

    static bool TryParseVersion(string versionText, out Version version)
    {
        version = null!;

        if (string.IsNullOrWhiteSpace(versionText))
            return false;

        if (versionText.Contains('.'))
            return Version.TryParse(versionText, out version!);

        if (versionText.Length == 1)
            return Version.TryParse($"{versionText}.0", out version!);

        var major = versionText[..^1];
        var minor = versionText[^1..];

        return Version.TryParse($"{major}.{minor}", out version!);
    }
}

/// <summary>
/// the types, per-file usings and per-file type aliases parsed from a set of source files.
/// </summary>
record SourceIndex(Dictionary<string, TypeInfo> Types,
                   Dictionary<string, List<string>> UsingsByFile,
                   Dictionary<string, Dictionary<string, string>> AliasesByFile);

record GeneratorCacheState(string Version,
                           string MainProjectHash,
                           bool ReferencedProjectsInspected,
                           string ReferencedProjectsHash,
                           bool NuGetPackagesInspected,
                           string NuGetPackagesHash,
                           string RootNamespace);

record FileParseResult(string FilePath,
                       List<string> FileUsings,
                       List<string> GlobalUsings,
                       Dictionary<string, string> FileTypeAliases,
                       Dictionary<string, string> GlobalTypeAliases,
                       Dictionary<string, TypeInfo> Types,
                       List<EndpointDeclaration> Endpoints);

/// <summary>
/// a class declaration whose base list matches an endpoint base type, recorded in document order.
/// </summary>
record EndpointDeclaration(string FilePath, string ClassName, string Namespace, string BaseType);

record TypeInfo(string FullName,
                string Namespace,
                string Name,
                string FilePath,
                List<PropertyInfo> Properties,
                List<TypeRef> BaseTypes,
                List<string> GenericParameters)
{
    internal static void AddOrMerge(Dictionary<string, TypeInfo> types, string fullName, TypeInfo typeInfo)
        => types[fullName] = types.TryGetValue(fullName, out var existing) ? existing.MergeWith(typeInfo) : typeInfo;

    internal TypeInfo MergeWith(TypeInfo incoming)
    {
        if (ReferenceEquals(this, incoming))
            return this;

        var mergedProps = Properties
                          .Concat(incoming.Properties)
                          .GroupBy(p => p.Name, StringComparer.Ordinal)
                          .Select(g => g.First())
                          .ToList();

        var mergedBaseTypes = BaseTypes
                              .Concat(incoming.BaseTypes)
                              .GroupBy(b => b.TypeName, StringComparer.Ordinal)
                              .Select(g => g.First())
                              .ToList();

        return this with
        {
            Properties = mergedProps,
            BaseTypes = mergedBaseTypes,
            GenericParameters = GenericParameters.Count >= incoming.GenericParameters.Count
                                    ? GenericParameters
                                    : incoming.GenericParameters
        };
    }
}

record PropertyInfo(string Name, string TypeName, string FilePath);

record TypeRef(string TypeName, string FilePath);

/// <summary>
/// the lexical scope a type name is resolved in.
/// </summary>
record TypeScope(string Namespace, string TypeFullName, List<string> Usings, Dictionary<string, string> Aliases);