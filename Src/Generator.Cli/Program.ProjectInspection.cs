using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static SourceFileSet CollectProjectSourceFilesWithHash(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var files = EnumerateProjectSourceFiles(projectDir);

        return new(files, ComputeContentHash(files, [projectPath]));
    }

    private static SourceFileSet CollectReferencedProjectSourceFilesWithHash(string projectPath)
    {
        var referencedProjectPaths = GetReferencedProjectPaths(projectPath);
        var files = new List<string>();

        foreach (var referencedProjectPath in referencedProjectPaths)
        {
            var referencedProjectDir = Path.GetDirectoryName(referencedProjectPath);

            if (referencedProjectDir == null)
                continue;

            files.AddRange(EnumerateProjectSourceFiles(referencedProjectDir));
        }

        return new(files, ComputeContentHash(files, referencedProjectPaths));
    }

    private static List<string> EnumerateProjectSourceFiles(string projectDir)
    {
        var separator = Path.DirectorySeparatorChar;
        var binSep = $"{separator}bin{separator}";
        var objSep = $"{separator}obj{separator}";

        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.Contains(binSep) && !file.Contains(objSep) && !file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                files.Add(file);
        }

        return files;
    }

    private static string ComputeContentHash(IEnumerable<string> files, IEnumerable<string>? additionalFiles = null)
    {
        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine(file);
            writer.WriteLine(File.GetLastWriteTimeUtc(file).Ticks);
        }

        if (additionalFiles != null)
        {
            foreach (var file in additionalFiles.Where(File.Exists).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteLine(file);
                writer.WriteLine(File.GetLastWriteTimeUtc(file).Ticks);
            }
        }

        writer.Flush();
        stream.Position = 0;

        return Convert.ToHexString(sha256.ComputeHash(stream));
    }

    private static bool IsUpToDate(string cacheFilePath,
                                   string mainProjectHash,
                                   string outputDir,
                                   Lazy<SourceFileSet> refSources,
                                   Lazy<NuGetPackageAssemblySet> nugetPackageAssemblies)
    {
        if (!File.Exists(cacheFilePath))
            return false;

        var contextPath = Path.Combine(outputDir, "SerializerContexts.g.cs");
        var extensionPath = Path.Combine(outputDir, "SerializerContextExtensions.g.cs");

        if (!File.Exists(contextPath) || !File.Exists(extensionPath))
            return false;

        var cacheState = ReadCacheState(cacheFilePath);

        if (cacheState == null || !string.Equals(cacheState.MainProjectHash, mainProjectHash, StringComparison.Ordinal))
            return false;

        if (!cacheState.ReferencedProjectsInspected)
        {
            if (!cacheState.NuGetPackagesInspected)
                return true;

            return string.Equals(cacheState.NuGetPackagesHash, nugetPackageAssemblies.Value.Hash, StringComparison.Ordinal);
        }

        if (!string.Equals(cacheState.ReferencedProjectsHash, refSources.Value.Hash, StringComparison.Ordinal))
            return false;

        return !cacheState.NuGetPackagesInspected ||
               string.Equals(cacheState.NuGetPackagesHash, nugetPackageAssemblies.Value.Hash, StringComparison.Ordinal);
    }

    private static string GetGeneratorOutputPath(string projectDir, string? customOutputPath)
    {
        if (!string.IsNullOrWhiteSpace(customOutputPath))
        {
            return Path.IsPathRooted(customOutputPath)
                       ? customOutputPath
                       : Path.Combine(projectDir, customOutputPath);
        }

        return Path.Combine(projectDir, "Generated", "FastEndpoints");
    }

    private static string? GetRootNamespace(string projectPath)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var rootNs = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "RootNamespace")?.Value;

            return rootNs?.Sanitize();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetTargetFramework(string projectPath)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var tfm = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value;

            if (!string.IsNullOrWhiteSpace(tfm))
                return tfm;

            var tfms = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value;

            return string.IsNullOrWhiteSpace(tfms)
                       ? null
                       : tfms.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static ReferencedProjectData LoadReferencedProjectContext(SourceFileSet referencedProjectSources)
    {
        if (referencedProjectSources.Files.Count == 0)
            return new(new(StringComparer.Ordinal), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), referencedProjectSources.Hash);

        var parseResults = ParseSourceFiles(referencedProjectSources.Files);
        var (_, typeDeclarations, allUsingsByFile, allTypeAliasesByFile) = BuildAnalysisInputs(parseResults);

        return new(typeDeclarations, allUsingsByFile, allTypeAliasesByFile, referencedProjectSources.Hash);
    }

    private static List<string> GetReferencedProjectPaths(string projectPath)
    {
        var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedProjects = new List<string>();

        CollectReferencedProjectPaths(projectPath, visitedProjects, referencedProjects);

        return referencedProjects;
    }

    private static void CollectReferencedProjectPaths(string projectPath, HashSet<string> visitedProjects, List<string> referencedProjects)
    {
        projectPath = Path.GetFullPath(projectPath);

        if (!visitedProjects.Add(projectPath) || !File.Exists(projectPath))
            return;

        XDocument doc;

        try
        {
            doc = XDocument.Load(projectPath);
        }
        catch
        {
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectReferences = doc.Descendants()
                                   .Where(e => e.Name.LocalName == "ProjectReference")
                                   .Select(e => e.Attribute("Include")?.Value)
                                   .Where(v => !string.IsNullOrWhiteSpace(v));

        foreach (var projectReference in projectReferences)
        {
            var normalizedReference = projectReference!
                                      .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                                      .Replace('\\', Path.DirectorySeparatorChar);
            var referencedProjectPath = Path.GetFullPath(Path.Combine(projectDir, normalizedReference));

            if (!File.Exists(referencedProjectPath) || visitedProjects.Contains(referencedProjectPath))
                continue;

            referencedProjects.Add(referencedProjectPath);
            CollectReferencedProjectPaths(referencedProjectPath, visitedProjects, referencedProjects);
        }
    }

    private static GeneratorCacheState? ReadCacheState(string cacheFilePath)
    {
        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var cacheState = JsonSerializer.Deserialize<GeneratorCacheState>(json);

            return cacheState?.Version == CacheSchemaVersion ? cacheState : null;
        }
        catch
        {
            return null;
        }
    }
}