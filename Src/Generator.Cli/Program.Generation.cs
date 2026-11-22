namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private const string CacheFileName = ".fastendpoints-generator-cache";
    private const string CacheSchemaVersion = "v3";

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
            () => CollectNuGetPackageCompileAssembliesWithHash(projectPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot));
        var nugetPackageLoader = new Lazy<NuGetPackageTypeLoader>(() => CreateNuGetPackageTypeLoader(nugetPackageAssemblies.Value));

        if (!forceRegenerate && IsUpToDate(cacheFilePath, hash, rootNamespace, outputDir, refSources, nugetPackageAssemblies))
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
            analysis.NuGetPackagesHash ?? string.Empty,
            rootNamespace);

        WriteGeneratedOutput(outputDir, rootNamespace, serializableTypes, cacheState, cacheFilePath, includeTimestamp);
        FlushDiagnostics();

        return 0;
    }
}