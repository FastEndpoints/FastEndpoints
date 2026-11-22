using System.Text.Json;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private const string CacheFileName = ".fastendpoints-generator-cache";
    private const string CacheSchemaVersion = "v4";
    private const string ContextFileName = "SerializerContexts.g.cs";
    private const string ExtensionsFileName = "SerializerContextExtensions.g.cs";

    private static bool IsUpToDate(string cacheFilePath,
                                   string mainProjectHash,
                                   string rootNamespace,
                                   string outputDir,
                                   Lazy<SourceFileSet> refSources,
                                   Lazy<NuGetPackageAssemblySet> nugetPackageAssemblies)
    {
        if (!File.Exists(cacheFilePath))
            return false;

        if (!File.Exists(Path.Combine(outputDir, ContextFileName)) || !File.Exists(Path.Combine(outputDir, ExtensionsFileName)))
            return false;

        var cacheState = ReadCacheState(cacheFilePath);

        if (cacheState == null ||
            !string.Equals(cacheState.MainProjectHash, mainProjectHash, StringComparison.Ordinal) ||
            !string.Equals(cacheState.RootNamespace, rootNamespace, StringComparison.Ordinal))
            return false;

        //only hash the external inputs that were actually inspected by the previous run
        return (!cacheState.ReferencedProjectsInspected ||
                string.Equals(cacheState.ReferencedProjectsHash, refSources.Value.Hash, StringComparison.Ordinal)) &&
               (!cacheState.NuGetPackagesInspected ||
                string.Equals(cacheState.NuGetPackagesHash, nugetPackageAssemblies.Value.Hash, StringComparison.Ordinal));
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

    //mirrors IsUpToDate: only record (and therefore only compute) the hashes of external inputs the analysis inspected
    private static GeneratorCacheState CreateCacheState(string mainProjectHash,
                                                        string rootNamespace,
                                                        AnalysisContext analysis,
                                                        Lazy<SourceFileSet> refSources,
                                                        Lazy<NuGetPackageAssemblySet> nugetPackageAssemblies)
        => new(
            CacheSchemaVersion,
            mainProjectHash,
            analysis.ReferencedProjectsInspected,
            analysis.ReferencedProjectsInspected ? refSources.Value.Hash : string.Empty,
            analysis.NuGetPackagesInspected,
            analysis.NuGetPackagesInspected ? nugetPackageAssemblies.Value.Hash : string.Empty,
            rootNamespace);

    //must run after the generated files are written, so a failed output write never leaves a valid cache behind
    private static void WriteCacheState(string cacheFilePath, GeneratorCacheState cacheState)
        => File.WriteAllText(cacheFilePath, JsonSerializer.Serialize(cacheState));
}
