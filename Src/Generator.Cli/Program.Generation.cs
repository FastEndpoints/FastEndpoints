namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private const string CacheFileName = ".fastendpoints-generator-cache";
    private const string CacheSchemaVersion = "v1";

    private static int ExecuteGenerator(string projectPath,
                                        bool forceRegenerate,
                                        string? customOutputPath,
                                        string? assetsFilePath,
                                        string? targetFramework,
                                        string? runtimeIdentifier,
                                        string? targetingPackRoot)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

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
            () => CollectNuGetPackageCompileAssembliesWithHash(projectPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot));
        var nugetPackageLoader = new Lazy<NuGetPackageTypeLoader>(() => CreateNuGetPackageTypeLoader(nugetPackageAssemblies.Value));

        if (!forceRegenerate && IsUpToDate(cacheFilePath, hash, outputDir, refSources, nugetPackageAssemblies))
        {
            Console.WriteLine("Generated files are up-to-date. Skipping generation.");

            return 0;
        }

        var parseResults = ParseSourceFiles(csFiles);
        var (syntaxTrees, typeDeclarations, allUsingsByFile, allTypeAliasesByFile) = BuildAnalysisInputs(parseResults);
        var analysis = AnalysisContext.Create(typeDeclarations, allUsingsByFile, allTypeAliasesByFile);
        analysis.SetReferencedProjectLoader(() => LoadReferencedProjectContext(refSources.Value));
        analysis.SetNuGetPackageLoader(() => nugetPackageLoader.Value);

        Console.WriteLine($"Found {typeDeclarations.Count} type declarations.");

        var (serializableTypes, endpointCount) = DiscoverSerializableTypesFromEndpoints(syntaxTrees, analysis);

        Console.WriteLine($"Found {endpointCount} endpoints with {serializableTypes.Count} serializable types.");

        if (serializableTypes.Count == 0)
        {
            Console.WriteLine("No serializable types found in endpoints.");
            FlushDiagnostics();

            return 0;
        }

        var cacheState = new GeneratorCacheState(
            CacheSchemaVersion,
            hash,
            analysis.ReferencedProjectsInspected,
            analysis.ReferencedProjectsHash ?? string.Empty,
            analysis.NuGetPackagesInspected,
            analysis.NuGetPackagesHash ?? string.Empty);

        WriteGeneratedOutput(outputDir, projectPath, projectName, serializableTypes, cacheState, cacheFilePath);
        FlushDiagnostics();

        return 0;
    }
}