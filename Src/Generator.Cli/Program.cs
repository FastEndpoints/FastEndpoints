namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static readonly Dictionary<string, string> _emptyAliases = new(StringComparer.Ordinal);
    private static readonly List<string> _emptyUsings = [];
    private static readonly GeneratorConfig _config = GeneratorConfig.Instance;

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: fastendpoints-generator <project-file-path> [--force] [--output <path>] [build-options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --force         Force regeneration even if files are up to date");
            Console.WriteLine("  --output <path> Custom output path for generated files");
            Console.WriteLine("  --assets-file <path>       Assets file path for package inspection (build integration)");
            Console.WriteLine("  --target-framework <tfm>   Target framework to inspect package metadata for (build integration)");
            Console.WriteLine("  --runtime-identifier <rid> Runtime identifier used for assets target selection (build integration)");
            Console.WriteLine("  --targeting-pack-root <path> Override targeting pack root used for metadata references (build integration)");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  fastendpoints-generator MyProject.csproj");
            Console.WriteLine("  fastendpoints-generator MyProject.csproj --output Generated");
            Console.WriteLine("  fastendpoints-generator MyProject.csproj --force");

            return 1;
        }

        var projectPath = Path.GetFullPath(args[0]);
        var forceRegenerate = args.Contains("--force", StringComparer.OrdinalIgnoreCase);

        var outputArgIndex = Array.IndexOf(args, "--output");
        var customOutputPath = outputArgIndex >= 0 && outputArgIndex + 1 < args.Length
                                   ? args[outputArgIndex + 1]
                                   : null;
        var assetsFilePath = GetOptionValue(args, "--assets-file");
        var targetFramework = GetOptionValue(args, "--target-framework");
        var runtimeIdentifier = GetOptionValue(args, "--runtime-identifier");
        var targetingPackRoot = GetOptionValue(args, "--targeting-pack-root");

        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Error: Project file not found: {projectPath}");

            return 1;
        }

        try
        {
            return ExecuteGenerator(projectPath, forceRegenerate, customOutputPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot);
        }
        catch (Exception ex)
        {
            FlushDiagnostics();
            Console.WriteLine($"Error: {ex.Message}");

            return 1;
        }
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        var optionIndex = Array.IndexOf(args, optionName);

        return optionIndex >= 0 && optionIndex + 1 < args.Length
                   ? args[optionIndex + 1]
                   : null;
    }
}