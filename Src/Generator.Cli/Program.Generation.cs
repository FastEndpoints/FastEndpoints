namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static int ExecuteGenerator(string projectPath,
                                        bool forceRegenerate,
                                        bool includeTimestamp,
                                        string? customOutputPath,
                                        string? assetsFilePath,
                                        string? targetFramework,
                                        string? runtimeIdentifier,
                                        string? targetingPackRoot,
                                        string? assemblyName)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

        //the source generators derive their namespace from the assembly name. this must produce the same value.
        var rootNamespace = (string.IsNullOrWhiteSpace(assemblyName) ? projectName : assemblyName).ToValidNameSpace();

        Console.WriteLine($"Analyzing project: {projectName}");

        var (csFiles, hash) = CollectProjectSourceFilesWithHash(projectPath);

        if (csFiles.Count == 0)
        {
            Console.WriteLine("No C# source files found.");

            return 0;
        }

        Console.WriteLine($"Found {csFiles.Count} source files.");

        var outputDir = GetGeneratorOutputPath(projectDir, customOutputPath);
        var cacheFilePath = Path.Combine(outputDir, CacheFileName);
        var refSources = new Lazy<SourceFileSet>(() => CollectReferencedProjectSourceFilesWithHash(projectPath));
        var nugetPackageAssemblies = new Lazy<NuGetPackageAssemblySet>(
            () => NuGetAssetsResolver.Resolve(projectPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot));

        if (!forceRegenerate && IsUpToDate(cacheFilePath, hash, rootNamespace, outputDir, refSources, nugetPackageAssemblies))
        {
            Console.WriteLine("Generated files are up-to-date. Skipping generation.");

            return 0;
        }

        var parseResults = ParseSourceFiles(csFiles);
        var (sourceIndex, endpoints) = BuildAnalysisInputs(parseResults);
        var analysis = new AnalysisContext(
            sourceIndex,
            () => BuildAnalysisInputs(ParseSourceFiles(refSources.Value.Files)).Index,
            () =>
            {
                var set = nugetPackageAssemblies.Value;

                return NuGetPackageTypeLoader.Create(set.PackageAssemblies, set.ReferenceAssemblies);
            });

        Console.WriteLine($"Found {sourceIndex.Types.Count} type declarations.");

        var serializableTypes = DiscoverSerializableTypesFromEndpoints(endpoints, analysis);

        Console.WriteLine($"Found {endpoints.Count} endpoints with {serializableTypes.Count} serializable types.");

        //still write the output (no context, no-op extension method), so files from an earlier run can't keep stale
        //typeof() references and the app's AddSerializerContextsFrom*() call keeps compiling.
        if (serializableTypes.Count == 0)
            Console.WriteLine("No serializable types found in endpoints. Writing a no-op serializer context extension.");

        WriteGeneratedOutput(outputDir, rootNamespace, serializableTypes, includeTimestamp);
        WriteCacheState(cacheFilePath, CreateCacheState(hash, rootNamespace, analysis, refSources, nugetPackageAssemblies));

        return 0;
    }
}