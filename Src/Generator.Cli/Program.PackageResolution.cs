using System.Text.Json;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static NuGetPackageAssemblySet CollectNuGetPackageCompileAssembliesWithHash(string projectPath,
                                                                                        string? assetsFilePath,
                                                                                        string? targetFramework,
                                                                                        string? runtimeIdentifier,
                                                                                        string? targetingPackRoot)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var resolvedAssetsFilePath = ResolveAssetsFilePath(projectDir, assetsFilePath);
        var resolvedTargetFramework = string.IsNullOrWhiteSpace(targetFramework) ? GetTargetFramework(projectPath) : targetFramework;

        if (resolvedAssetsFilePath == null || !File.Exists(resolvedAssetsFilePath))
        {
            if (!string.IsNullOrWhiteSpace(targetFramework) || !string.IsNullOrWhiteSpace(assetsFilePath))
                ReportDiagnostic($"Warning: assets file was not found at '{resolvedAssetsFilePath ?? "<unknown>"}'. NuGet package DTO resolution will be skipped.");

            return new([], [], string.Empty);
        }

        var packageAssemblies = ReadNuGetPackageCompileAssemblies(resolvedAssetsFilePath, resolvedTargetFramework, runtimeIdentifier);
        var assemblyPaths = packageAssemblies.Select(a => a.AssemblyPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var referenceAssemblies = GetTargetFrameworkReferenceAssemblies(resolvedTargetFramework, targetingPackRoot);
        var hashInputs = new List<string> { resolvedAssetsFilePath };
        hashInputs.AddRange(referenceAssemblies);

        return new(packageAssemblies, referenceAssemblies, ComputeFileSetHash(assemblyPaths, hashInputs));
    }

    private static string? ResolveAssetsFilePath(string projectDir, string? assetsFilePath)
    {
        if (!string.IsNullOrWhiteSpace(assetsFilePath))
        {
            return Path.IsPathRooted(assetsFilePath)
                       ? assetsFilePath
                       : Path.GetFullPath(Path.Combine(projectDir, assetsFilePath));
        }

        var defaultAssetsFilePath = Path.Combine(projectDir, "obj", "project.assets.json");

        return File.Exists(defaultAssetsFilePath) ? defaultAssetsFilePath : null;
    }

    private static List<NuGetPackageAssemblyInfo> ReadNuGetPackageCompileAssemblies(string assetsFilePath, string? targetFramework, string? runtimeIdentifier)
    {
        using var stream = File.OpenRead(assetsFilePath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("targets", out var targetsElement) || targetsElement.ValueKind != JsonValueKind.Object)
        {
            ReportDiagnostic($"Warning: assets file '{assetsFilePath}' does not contain a valid 'targets' section. NuGet package DTO resolution will be skipped.");

            return [];
        }

        var targetKey = SelectAssetsTargetKey(targetsElement, targetFramework, runtimeIdentifier);

        if (targetKey == null || !targetsElement.TryGetProperty(targetKey, out var targetElement) || targetElement.ValueKind != JsonValueKind.Object)
        {
            ReportDiagnostic($"Warning: unable to select a valid assets target graph from '{assetsFilePath}'. NuGet package DTO resolution will be skipped.");

            return [];
        }

        var packageFolders = ReadPackageFolders(document.RootElement);

        if (packageFolders.Count == 0)
        {
            ReportDiagnostic($"Warning: assets file '{assetsFilePath}' does not contain any package folders. NuGet package DTO resolution will be skipped.");

            return [];
        }

        var packageAssemblies = new List<NuGetPackageAssemblyInfo>();

        foreach (var libraryEntry in targetElement.EnumerateObject())
        {
            var libraryKey = libraryEntry.Name;
            var libraryValue = libraryEntry.Value;

            if (!IsPackageTarget(libraryValue))
                continue;

            if (!TryGetPackagePath(document.RootElement, libraryKey, out var packagePath))
                continue;

            if (!libraryValue.TryGetProperty("compile", out var compileElement) || compileElement.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var compileEntry in compileElement.EnumerateObject())
            {
                if (!compileEntry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                var assemblyPath = ResolvePackageFilePath(packageFolders, packagePath, compileEntry.Name);

                if (assemblyPath == null)
                {
                    ReportDiagnostic($"Warning: package assembly '{compileEntry.Name}' for '{libraryKey}' was not found under the configured NuGet package folders.");

                    continue;
                }

                packageAssemblies.Add(new(libraryKey, assemblyPath));
            }
        }

        return packageAssemblies;
    }

    private static string? SelectAssetsTargetKey(JsonElement targetsElement, string? targetFramework, string? runtimeIdentifier)
    {
        var targetNames = targetsElement.EnumerateObject()
                                        .Select(t => t.Name)
                                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                                        .ToList();

        if (targetNames.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            if (targetNames.Count == 1)
                return targetNames[0];

            var fallback = targetNames[0];
            ReportDiagnostic($"Warning: multiple assets target graphs were found ({string.Join(", ", targetNames)}), but no target framework was provided. Using '{fallback}'.");

            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            var exactRidMatch = $"{targetFramework}/{runtimeIdentifier}";

            if (targetsElement.TryGetProperty(exactRidMatch, out _))
                return exactRidMatch;
        }

        if (targetsElement.TryGetProperty(targetFramework, out _))
            return targetFramework;

        var requestedMatches = SelectAssetsTargetMatches(targetNames, targetFramework, runtimeIdentifier);
        var requestedMatch = ChooseAssetsTargetCandidate(requestedMatches, targetFramework, runtimeIdentifier, "requested target framework");

        if (requestedMatch != null)
            return requestedMatch;

        var normalizedTargetFramework = NormalizeTargetFrameworkMoniker(targetFramework);

        if (!string.IsNullOrWhiteSpace(normalizedTargetFramework) && !string.Equals(normalizedTargetFramework, targetFramework, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                var normalizedRidMatch = $"{normalizedTargetFramework}/{runtimeIdentifier}";

                if (targetsElement.TryGetProperty(normalizedRidMatch, out _))
                {
                    ReportDiagnostic(
                        $"Warning: assets target graph '{targetFramework}/{runtimeIdentifier}' was not found. Falling back to compatible graph '{normalizedRidMatch}'.");

                    return normalizedRidMatch;
                }
            }

            if (targetsElement.TryGetProperty(normalizedTargetFramework, out _))
            {
                ReportDiagnostic($"Warning: assets target graph '{targetFramework}' was not found. Falling back to compatible graph '{normalizedTargetFramework}'.");

                return normalizedTargetFramework;
            }

            var normalizedMatches = SelectAssetsTargetMatches(targetNames, normalizedTargetFramework, runtimeIdentifier);
            var normalizedMatch = ChooseAssetsTargetCandidate(normalizedMatches, normalizedTargetFramework, runtimeIdentifier, "compatible target framework");

            if (normalizedMatch != null)
            {
                ReportDiagnostic($"Warning: assets target graph '{targetFramework}' was not found. Falling back to compatible graph '{normalizedMatch}'.");

                return normalizedMatch;
            }
        }

        ReportDiagnostic(
            $"Warning: unable to confidently select an assets target graph for target framework '{targetFramework}'{FormatRuntimeIdentifier(runtimeIdentifier)}. Available graphs: {string.Join(", ", targetNames)}.");

        return null;
    }

    private static List<string> SelectAssetsTargetMatches(List<string> targetNames, string targetFramework, string? runtimeIdentifier)
    {
        var matches = targetNames.Where(name => MatchesAssetsTargetFramework(GetAssetsTargetFramework(name), targetFramework));

        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            var runtimeMatches = matches.Where(name => string.Equals(GetAssetsTargetRuntimeIdentifier(name), runtimeIdentifier, StringComparison.OrdinalIgnoreCase)).ToList();

            if (runtimeMatches.Count > 0)
                return runtimeMatches;
        }

        return matches.Where(name => string.IsNullOrWhiteSpace(GetAssetsTargetRuntimeIdentifier(name))).ToList();
    }

    private static string? ChooseAssetsTargetCandidate(List<string> candidates, string targetFramework, string? runtimeIdentifier, string reason)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        ReportDiagnostic(
            $"Warning: multiple assets target graphs matched the {reason} '{targetFramework}'{FormatRuntimeIdentifier(runtimeIdentifier)}: {string.Join(", ", candidates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}. Use an exact target framework/runtime identifier so package DTO resolution can select the correct graph.");

        return null;
    }

    private static string GetAssetsTargetFramework(string targetName)
    {
        var separatorIndex = targetName.IndexOf('/');

        return separatorIndex >= 0 ? targetName[..separatorIndex] : targetName;
    }

    private static string? GetAssetsTargetRuntimeIdentifier(string targetName)
    {
        var separatorIndex = targetName.IndexOf('/');

        return separatorIndex >= 0 && separatorIndex < targetName.Length - 1
                   ? targetName[(separatorIndex + 1)..]
                   : null;
    }

    private static bool MatchesAssetsTargetFramework(string assetTargetFramework, string requestedTargetFramework)
    {
        if (string.Equals(assetTargetFramework, requestedTargetFramework, StringComparison.OrdinalIgnoreCase))
            return true;

        return assetTargetFramework.StartsWith(requestedTargetFramework, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ReadPackageFolders(JsonElement root)
    {
        if (!root.TryGetProperty("packageFolders", out var packageFoldersElement) || packageFoldersElement.ValueKind != JsonValueKind.Object)
            return [];

        var packageFolders = new List<string>();

        foreach (var folder in packageFoldersElement.EnumerateObject())
        {
            var path = folder.Name
                             .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);

            if (!path.EndsWith(Path.DirectorySeparatorChar))
                path += Path.DirectorySeparatorChar;

            packageFolders.Add(path);
        }

        return packageFolders;
    }

    private static bool IsPackageTarget(JsonElement libraryValue)
        => libraryValue.TryGetProperty("type", out var typeElement) &&
           string.Equals(typeElement.GetString(), "package", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetPackagePath(JsonElement root, string libraryKey, out string packagePath)
    {
        packagePath = string.Empty;

        if (!root.TryGetProperty("libraries", out var librariesElement) || librariesElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!librariesElement.TryGetProperty(libraryKey, out var libraryElement) || libraryElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!libraryElement.TryGetProperty("path", out var pathElement))
            return false;

        packagePath = pathElement.GetString() ?? string.Empty;

        return packagePath.Length > 0;
    }

    private static string? ResolvePackageFilePath(IEnumerable<string> packageFolders, string packagePath, string relativeFilePath)
    {
        var normalizedPackagePath = packagePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var normalizedRelativeFilePath = relativeFilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        foreach (var packageFolder in packageFolders)
        {
            var fullPath = Path.Combine(packageFolder, normalizedPackagePath, normalizedRelativeFilePath);

            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private static List<string> GetTargetFrameworkReferenceAssemblies(string? targetFramework, string? targetingPackRoot)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return [];

        var refAssemblyPaths = new List<string>();
        var packRoot = string.IsNullOrWhiteSpace(targetingPackRoot)
                           ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "packs")
                           : targetingPackRoot;

        AddReferenceAssemblies(refAssemblyPaths, packRoot, "Microsoft.NETCore.App.Ref", targetFramework);
        AddReferenceAssemblies(refAssemblyPaths, packRoot, "Microsoft.AspNetCore.App.Ref", targetFramework);

        var netStandardDir = Path.Combine(packRoot, "NETStandard.Library.Ref");

        if (Directory.Exists(netStandardDir))
        {
            var latestVersionDir = Directory.EnumerateDirectories(netStandardDir).OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

            if (latestVersionDir != null)
            {
                var refDir = Path.Combine(latestVersionDir, "ref", "netstandard2.1");

                if (Directory.Exists(refDir))
                    refAssemblyPaths.AddRange(Directory.EnumerateFiles(refDir, "*.dll", SearchOption.TopDirectoryOnly));
            }
        }

        refAssemblyPaths = refAssemblyPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (refAssemblyPaths.Count == 0)
        {
            ReportDiagnostic(
                $"Warning: no framework reference assemblies were found for target framework '{targetFramework}' under '{packRoot}'. NuGet package DTO resolution may be incomplete.");
        }

        return refAssemblyPaths;
    }

    private static void AddReferenceAssemblies(List<string> paths, string packRoot, string packName, string targetFramework)
    {
        var packDir = Path.Combine(packRoot, packName);

        if (!Directory.Exists(packDir))
            return;

        var latestVersionDir = Directory.EnumerateDirectories(packDir).OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        if (latestVersionDir == null)
            return;

        var refBaseDir = Path.Combine(latestVersionDir, "ref");

        if (!Directory.Exists(refBaseDir))
            return;

        var refDir = ResolveReferenceFrameworkDirectory(refBaseDir, targetFramework);

        if (refDir == null)
        {
            ReportDiagnostic($"Warning: no compatible reference pack folder was found for '{packName}' and target framework '{targetFramework}'.");

            return;
        }

        paths.AddRange(Directory.EnumerateFiles(refDir, "*.dll", SearchOption.TopDirectoryOnly));
    }

    internal static string? ResolveReferenceFrameworkDirectory(string refBaseDir, string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(refBaseDir) || string.IsNullOrWhiteSpace(targetFramework) || !Directory.Exists(refBaseDir))
            return null;

        var directories = Directory.EnumerateDirectories(refBaseDir).ToList();

        if (directories.Count == 0)
            return null;

        var exactMatch = directories.FirstOrDefault(d => string.Equals(Path.GetFileName(d), targetFramework, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
            return exactMatch;

        if (!TryParseTargetFrameworkMoniker(targetFramework, out var requestedMoniker))
            return null;

        var normalizedTargetFramework = requestedMoniker.BaseMoniker;

        var normalizedMatch = directories.FirstOrDefault(d => string.Equals(Path.GetFileName(d), normalizedTargetFramework, StringComparison.OrdinalIgnoreCase));

        if (normalizedMatch != null)
            return normalizedMatch;

        return directories.Where(d => TryParseTargetFrameworkMoniker(Path.GetFileName(d), out _))
                          .Select(d => new { Path = d, Moniker = ParseTargetFrameworkMoniker(Path.GetFileName(d)) })
                          .Where(x => x.Moniker is not null && IsCompatibleReferenceTarget(requestedMoniker, x.Moniker.Value))
                          .OrderByDescending(x => x.Moniker!.Value.Version)
                          .Select(x => x.Path)
                          .FirstOrDefault();
    }

    private static bool IsCompatibleReferenceTarget(TargetFrameworkMoniker requested, TargetFrameworkMoniker candidate)
        => string.Equals(requested.Identifier, candidate.Identifier, StringComparison.OrdinalIgnoreCase) &&
           candidate.Version <= requested.Version;

    internal static string? NormalizeTargetFrameworkMoniker(string? targetFramework)
        => !string.IsNullOrWhiteSpace(targetFramework) && TryParseTargetFrameworkMoniker(targetFramework, out var moniker)
               ? moniker.BaseMoniker
               : null;

    private static string FormatRuntimeIdentifier(string? runtimeIdentifier)
        => string.IsNullOrWhiteSpace(runtimeIdentifier) ? string.Empty : $" and runtime identifier '{runtimeIdentifier}'";

    internal static bool TryParseTargetFrameworkMoniker(string targetFramework, out TargetFrameworkMoniker moniker)
    {
        moniker = new(string.Empty, new(0, 0));

        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        var tfm = targetFramework.Trim();
        var separatorIndex = tfm.IndexOf('-');

        if (separatorIndex >= 0)
            tfm = tfm[..separatorIndex];

        if (!TryExtractFrameworkIdentifierAndVersion(tfm, out var identifier, out var versionText))
            return false;

        if (!TryParseFrameworkVersion(versionText, out var version))
            return false;

        moniker = new(identifier, version);

        return true;
    }

    private static TargetFrameworkMoniker? ParseTargetFrameworkMoniker(string? targetFramework)
        => TryParseTargetFrameworkMoniker(targetFramework ?? string.Empty, out var moniker) ? moniker : null;

    private static bool TryExtractFrameworkIdentifierAndVersion(string targetFramework,
                                                                out string identifier,
                                                                out string versionText)
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

    private static bool TryParseFrameworkVersion(string versionText, out Version version)
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

    private static NuGetPackageTypeLoader CreateNuGetPackageTypeLoader(NuGetPackageAssemblySet packageAssemblies)
        => NuGetPackageTypeLoader.Create(packageAssemblies.PackageAssemblies, packageAssemblies.ReferenceAssemblies, packageAssemblies.Hash);
}