using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static readonly Dictionary<string, string> _emptyAliases = new(StringComparer.Ordinal);
    private static readonly List<string> _emptyUsings = [];
    private static readonly GeneratorConfig _config = GeneratorConfig.Instance;

    private static readonly HashSet<string> _primitiveTypeNames = new(StringComparer.Ordinal)
    {
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "dynamic",
        "float",
        "int",
        "long",
        "nint",
        "nuint",
        "object",
        "sbyte",
        "short",
        "string",
        "uint",
        "ulong",
        "ushort"
    };

    private static readonly Dictionary<string, string> _rootTypeCache = new(StringComparer.Ordinal);

    private const string CacheFileName = ".fastendpoints-generator-cache";
    private const string CacheSchemaVersion = "v2";

    private sealed class GeneratorConfig
    {
        public static readonly GeneratorConfig Instance = new();

        public HashSet<string> RootTypeSkipNames { get; } = new(StringComparer.Ordinal)
        {
            "PlainTextRequest"
        };

        public string[] EndpointBaseTypePatterns { get; } =
        [
            "Endpoint",
            "EndpointWithoutRequest",
            "EndpointWithMapper",
            "EndpointWithMapping",
            "Ep"
        ];

        public string[] ExcludedBaseTypes { get; } =
        [
            "Mapper",
            "Validator",
            "AbstractValidator",
            "Summary",
            "EndpointSummary",
            "ICommand",
            "ICommandHandler",
            "IEvent",
            "IEventHandler"
        ];

        public string[] SkipNamespaces { get; } =
        [
            "Accessibility",
            "FastEndpoints",
            "FluentValidation",
            "Grpc",
            "JetBrains",
            "Microsoft",
            "mscorlib",
            "Namotion",
            "netstandard",
            "Newtonsoft",
            "NJsonSchema",
            "NSwag",
            "NuGet",
            "PresentationCore",
            "PresentationFramework",
            "StackExchange",
            "System",
            "testhost",
            "WindowsBase",
            "YamlDotNet"
        ];

        public string[] SkipTypes { get; } =
        [
            "EmptyRequest",
            "EmptyResponse"
        ];

        public HashSet<string> BuiltInCollectionTypes { get; } = new(StringComparer.Ordinal)
        {
            "Dictionary",
            "IEnumerable",
            "List",
            "ICollection",
            "IDictionary",
            "IList",
            "IReadOnlyDictionary",
            "IReadOnlyList",
            "IReadOnlyCollection",
            "HashSet",
            "SortedSet",
            "Stack",
            "Queue"
        };

        private GeneratorConfig() { }
    }

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: fastendpoints-generator <project-file-path> [--force] [--output <path>]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --force         Force regeneration even if files are up to date");
            Console.WriteLine("  --output <path> Custom output path for generated files");
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
            return RunGenerator(projectPath, forceRegenerate, customOutputPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot);
        }
        catch (Exception ex)
        {
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

    private static int RunGenerator(string projectPath,
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

        var (csFiles, hash) = GetProjectSourceFilesWithHash(projectPath);

        if (csFiles.Count == 0)
        {
            Console.WriteLine("No C# source files found.");

            return 0;
        }

        Console.WriteLine($"Found {csFiles.Count} source files.");

        var outputDir = GetGeneratorOutputPath(projectDir, customOutputPath);
        var cacheFilePath = Path.Combine(outputDir, CacheFileName);
        var refSources = new Lazy<SourceFileSet>(() => GetReferencedProjectSourceFilesWithHash(projectPath));
        var nugetPackageAssemblies = new Lazy<NuGetPackageAssemblySet>(() => GetNuGetPackageCompileAssembliesWithHash(projectPath, assetsFilePath, targetFramework, runtimeIdentifier, targetingPackRoot));
        var nugetPackageLoader = new Lazy<NuGetPackageTypeLoader>(() => LoadNuGetPackageTypeLoader(nugetPackageAssemblies.Value));

        if (!forceRegenerate && IsUpToDate(cacheFilePath, hash, outputDir, refSources, nugetPackageAssemblies))
        {
            Console.WriteLine("Generated files are up-to-date. Skipping generation.");

            return 0;
        }

        var parseResults = ParseAllFiles(csFiles);
        var (syntaxTrees, typeDeclarations, allUsingsByFile, allTypeAliasesByFile) = AggregateResults(parseResults);
        var analysis = AnalysisContext.Create(typeDeclarations, allUsingsByFile, allTypeAliasesByFile);
        analysis.SetReferencedProjectLoader(() => LoadReferencedProjectData(refSources.Value));
        analysis.SetNuGetPackageLoader(() => nugetPackageLoader.Value);

        Console.WriteLine($"Found {typeDeclarations.Count} type declarations.");

        var (serializableTypes, endpointCount) = AnalyzeEndpoints(syntaxTrees, analysis);

        Console.WriteLine($"Found {endpointCount} endpoints with {serializableTypes.Count} serializable types.");

        if (serializableTypes.Count == 0)
        {
            Console.WriteLine("No serializable types found in endpoints.");

            return 0;
        }

        var cacheState = new GeneratorCacheState(
            CacheSchemaVersion,
            hash,
            analysis.ReferencedProjectsInspected,
            analysis.ReferencedProjectsHash ?? string.Empty,
            analysis.NuGetPackagesInspected,
            analysis.NuGetPackagesHash ?? string.Empty);

        GenerateOutput(outputDir, projectPath, projectName, serializableTypes, cacheState, cacheFilePath);

        return 0;
    }

    private static ConcurrentBag<FileParseResult> ParseAllFiles(List<string> csFiles)
    {
        var parseResults = new ConcurrentBag<FileParseResult>();

        Parallel.ForEach(
            csFiles,
            file =>
            {
                var result = ParseFile(file);
                parseResults.Add(result);
            });

        return parseResults;
    }

    private static (List<(SyntaxTree Tree, string FilePath)> SyntaxTrees, Dictionary<string, TypeInfo> TypeDeclarations, Dictionary<string, List<string>> AllUsingsByFile,
        Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile) AggregateResults(ConcurrentBag<FileParseResult> parseResults)
    {
        var globalUsings = new List<string>();
        var globalTypeAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignoredGlobalAliases = new HashSet<string>(StringComparer.Ordinal);
        var typeDeclarations = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);
        var usingsByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var typeAliasesByFile = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var syntaxTrees = new List<(SyntaxTree Tree, string FilePath)>();

        foreach (var result in parseResults)
        {
            syntaxTrees.Add((result.Tree, result.FilePath));
            usingsByFile[result.FilePath] = result.FileUsings;
            typeAliasesByFile[result.FilePath] = result.FileTypeAliases;

            foreach (var globalUsing in result.GlobalUsings)
            {
                if (!globalUsings.Contains(globalUsing))
                    globalUsings.Add(globalUsing);
            }

            foreach (var kvp in result.GlobalTypeAliases)
            {
                if (ignoredGlobalAliases.Contains(kvp.Key))
                    continue;

                if (globalTypeAliases.TryGetValue(kvp.Key, out var existingTarget))
                {
                    if (!string.Equals(existingTarget, kvp.Value, StringComparison.Ordinal))
                    {
                        globalTypeAliases.Remove(kvp.Key);
                        ignoredGlobalAliases.Add(kvp.Key);
                    }
                }
                else
                    globalTypeAliases[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in result.Types)
            {
                if (typeDeclarations.TryGetValue(kvp.Key, out var existingType))
                    typeDeclarations[kvp.Key] = MergeTypeInfos(existingType, kvp.Value);
                else
                    typeDeclarations[kvp.Key] = kvp.Value;
            }
        }

        var allUsingsByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fp, fileUsings) in usingsByFile)
            allUsingsByFile[fp] = fileUsings.Concat(globalUsings).Distinct().ToList();

        var allTypeAliasesByFile = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fp in usingsByFile.Keys)
        {
            var map = new Dictionary<string, string>(globalTypeAliases, StringComparer.Ordinal);

            if (typeAliasesByFile.TryGetValue(fp, out var fileAliases))
            {
                foreach (var kvp in fileAliases)
                    map[kvp.Key] = kvp.Value;
            }
            allTypeAliasesByFile[fp] = map;
        }

        return (syntaxTrees, typeDeclarations, allUsingsByFile, allTypeAliasesByFile);
    }

    private static (HashSet<string> SerializableTypes, int EndpointCount) AnalyzeEndpoints(List<(SyntaxTree Tree, string FilePath)> syntaxTrees, AnalysisContext analysis)
    {
        var serializableTypes = new HashSet<string>(StringComparer.Ordinal);
        var endpointCount = 0;

        foreach (var (tree, filePath) in syntaxTrees)
        {
            var root = tree.GetRoot();

            var allUsings = analysis.AllUsingsByFile.GetValueOrDefault(filePath) ?? _emptyUsings;
            var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(filePath) ?? _emptyAliases;

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.Modifiers.Any(m => m.Text == "file"))
                    continue;

                var baseTypes = GetBaseTypes(classDecl);
                var endpointBase = FindEndpointBaseType(baseTypes);

                if (endpointBase == null)
                    continue;

                endpointCount++;
                var className = GetFullTypeName(classDecl);
                Console.WriteLine($"  Found endpoint: {className}");

                var currentNs = GetContainingNamespace(classDecl);

                var typeArgs = IsFluentEndpointBaseType(endpointBase)
                                   ? ExtractFluentTypeArguments(endpointBase)
                                   : ExtractTypeArguments(endpointBase);

                foreach (var typeArg in typeArgs)
                    ProcessTypeExpression(typeArg, currentNs, className, allUsings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
            }
        }

        return (serializableTypes, endpointCount);
    }

    private static void GenerateOutput(string outputDir,
                                       string projectPath,
                                       string projectName,
                                       HashSet<string> serializableTypes,
                                       GeneratorCacheState cacheState,
                                       string cacheFilePath)
    {
        Directory.CreateDirectory(outputDir);

        var rootNamespace = GetRootNamespace(projectPath) ?? projectName.Sanitize();
        var contextCode = GenerateSerializerContext(rootNamespace, serializableTypes);
        var contextPath = Path.Combine(outputDir, "SerializerContexts.g.cs");
        File.WriteAllText(contextPath, contextCode, Encoding.UTF8);

        var extensionCode = GenerateExtensionMethod(rootNamespace);
        var extensionPath = Path.Combine(outputDir, "SerializerContextExtensions.g.cs");
        File.WriteAllText(extensionPath, extensionCode, Encoding.UTF8);

        File.WriteAllText(cacheFilePath, JsonSerializer.Serialize(cacheState));

        Console.WriteLine("Generated files:");
        Console.WriteLine($"  {contextPath}");
        Console.WriteLine($"  {extensionPath}");
    }

    private static SourceFileSet GetProjectSourceFilesWithHash(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var files = EnumerateProjectSourceFiles(projectDir);

        return new(files, ComputeContentHash(files, [projectPath]));
    }

    private static SourceFileSet GetReferencedProjectSourceFilesWithHash(string projectPath)
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

    private static FileParseResult ParseFile(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
        var root = tree.GetRoot();

        var fileUsings = new List<string>();
        var globalUsings = new List<string>();
        var fileTypeAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var globalTypeAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var types = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);

        var allNodes = root.DescendantNodes().ToList();

        foreach (var node in allNodes)
        {
            if (node is UsingDirectiveSyntax usingDirective)
            {
                if (usingDirective.Name == null)
                    continue;

                if (usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
                    continue;

                var usingName = usingDirective.Name.ToString();

                if (usingDirective.Alias != null)
                {
                    var aliasName = usingDirective.Alias.Name.ToString();
                    if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                        globalTypeAliases[aliasName] = usingName;
                    else
                        fileTypeAliases[aliasName] = usingName;

                    continue;
                }

                if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                    globalUsings.Add(usingName);
                else
                    fileUsings.Add(usingName);
            }
            else if (node is TypeDeclarationSyntax typeDecl)
            {
                if (typeDecl.Modifiers.Any(m => m.Text == "file"))
                    continue;

                var fullName = GetFullTypeName(typeDecl);
                var baseTypes = typeDecl.BaseList?.Types.Select(t => new TypeRef(t.ToString(), filePath)).ToList() ?? [];
                var properties = new List<PropertyInfo>();

                foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
                    properties.Add(new(prop.Identifier.Text, prop.Type.ToString(), filePath));

                if (typeDecl is RecordDeclarationSyntax { ParameterList: not null } recordDecl)
                {
                    foreach (var param in recordDecl.ParameterList.Parameters)
                    {
                        if (param.Type != null)
                            properties.Add(new(param.Identifier.Text, param.Type.ToString(), filePath));
                    }
                }

                var ns = "";
                var parent = typeDecl.Parent;

                while (parent != null)
                {
                    if (parent is BaseNamespaceDeclarationSyntax nsDecl)
                    {
                        ns = nsDecl.Name.ToString();

                        break;
                    }
                    parent = parent.Parent;
                }

                var genericParameters = typeDecl.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? [];
                var typeInfo = new TypeInfo(fullName, ns, typeDecl.Identifier.Text, filePath, properties, baseTypes, genericParameters);
                if (types.TryGetValue(fullName, out var existing))
                    types[fullName] = MergeTypeInfos(existing, typeInfo);
                else
                    types[fullName] = typeInfo;
            }
        }

        return new(filePath, tree, fileUsings, globalUsings, fileTypeAliases, globalTypeAliases, types);
    }

    private static List<string> GetBaseTypes(ClassDeclarationSyntax classDecl)
        => classDecl.BaseList == null
               ? []
               : classDecl.BaseList.Types.Select(t => t.ToString()).ToList();

    private static string? FindEndpointBaseType(List<string> baseTypes)
    {
        foreach (var baseType in baseTypes)
        {
            if (IsFluentEndpointBaseType(baseType))
                return baseType;

            var baseSpan = baseType.AsSpan();
            var genIdx = baseSpan.IndexOf('<');
            if (genIdx >= 0)
                baseSpan = baseSpan[..genIdx];

            var dotIdx = baseSpan.LastIndexOf('.');
            var baseName = dotIdx >= 0 ? baseSpan[(dotIdx + 1)..].Trim() : baseSpan.Trim();

            foreach (var pattern in _config.EndpointBaseTypePatterns)
            {
                if (baseName.StartsWith(pattern.AsSpan(), StringComparison.Ordinal))
                    return baseType;
            }
        }

        return null;
    }

    private static bool IsFluentEndpointBaseType(string baseType)
    {
        var span = baseType.AsSpan();
        var genericIdx = span.IndexOf('<');

        if (genericIdx > 0)
            span = span[..genericIdx];

        span = span.Trim();

        return span.StartsWith("Ep.", StringComparison.Ordinal) ||
               span.Contains(".Ep.", StringComparison.Ordinal);
    }

    private static List<string> ExtractTypeArguments(string genericType)
    {
        var result = new List<string>();
        var match = TypeArgMatcherRegex().Match(genericType);

        if (match.Success)
        {
            var argsString = match.Groups[1].Value;
            var args = SplitTypeArguments(argsString);
            result.AddRange(args);
        }

        return result;
    }

    private static List<string> ExtractFluentTypeArguments(string fluentBaseType)
    {
        var result = new List<string>();

        foreach (var segment in SplitDotSegments(fluentBaseType))
        {
            var span = segment.AsSpan();
            var genericIdx = span.IndexOf('<');
            var segName = genericIdx > 0 ? span[..genericIdx].Trim() : span.Trim();

            // only extract type args from Req<T> and Res<T> segments; skip Ep, NoReq, NoRes, Map
            if (!segName.Equals("Req", StringComparison.Ordinal) && !segName.Equals("Res", StringComparison.Ordinal))
                continue;

            result.AddRange(ExtractTypeArguments(segment));
        }

        return result;
    }

    private static List<string> SplitBalanced(string input, char delimiter, bool trimItems = false)
    {
        var result = new List<string>();
        var depth = 0;
        var lastSplit = 0;

        for (var i = 0; i < input.Length; i++)
        {
            switch (input[i])
            {
                case '<':
                    depth++;

                    break;
                case '>':
                    depth--;

                    break;
                default:
                    if (depth == 0 && input[i] == delimiter)
                    {
                        var item = trimItems ? input[lastSplit..i].Trim() : input[lastSplit..i];
                        if (item.Length > 0)
                            result.Add(item);
                        lastSplit = i + 1;
                    }

                    break;
            }
        }

        if (lastSplit < input.Length)
        {
            var item = trimItems ? input[lastSplit..].Trim() : input[lastSplit..];
            if (item.Length > 0)
                result.Add(item);
        }

        return result;
    }

    private static List<string> SplitDotSegments(string typeName)
        => SplitBalanced(typeName, '.');

    private static List<string> SplitTypeArguments(string argsString)
        => SplitBalanced(argsString, ',', true);

    private static string? ResolveTypeName(string typeName,
                                           string currentNamespace,
                                           string currentTypeFullName,
                                           List<string> usings,
                                           Dictionary<string, string> typeAliases,
                                           Dictionary<string, TypeInfo> types,
                                           Dictionary<string, string>? simpleNameCache = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        typeName = typeName.Trim();
        typeName = typeName.TrimEnd('?');

        if (typeName.StartsWith("global::", StringComparison.Ordinal))
            typeName = typeName[8..];

        if (typeAliases.Count > 0)
            typeName = ExpandAliasInTypeName(typeName, typeAliases);

        var lookupName = ExtractRootType(typeName);

        if (types.ContainsKey(typeName))
            return typeName;

        if (!string.Equals(lookupName, typeName, StringComparison.Ordinal) && types.ContainsKey(lookupName))
            return lookupName;

        if (!string.IsNullOrEmpty(currentTypeFullName) && lookupName.IndexOf('.') < 0)
        {
            foreach (var candidate in GetNestedTypeCandidates(currentTypeFullName, lookupName))
            {
                if (types.ContainsKey(candidate))
                    return candidate;
            }
        }

        if (!string.IsNullOrEmpty(currentNamespace))
        {
            var fullName = $"{currentNamespace}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        foreach (var usingNs in usings)
        {
            var fullName = $"{usingNs}.{lookupName}";

            if (types.ContainsKey(fullName))
                return fullName;
        }

        if (simpleNameCache != null && simpleNameCache.TryGetValue(lookupName, out var cached))
            return cached;

        return null;
    }

    private static IEnumerable<string> GetNestedTypeCandidates(string currentTypeFullName, string lookupName)
    {
        var parts = currentTypeFullName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        for (var i = parts.Length; i >= 1; i--)
        {
            var prefix = string.Join('.', parts.Take(i));

            yield return $"{prefix}.{lookupName}";
        }
    }

    private static string GetFullTypeName(TypeDeclarationSyntax typeDecl)
    {
        var nameParts = new List<string> { typeDecl.Identifier.Text };
        var parent = typeDecl.Parent;

        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax nsDecl)
            {
                nameParts.Insert(0, nsDecl.Name.ToString());

                break;
            }

            if (parent is TypeDeclarationSyntax parentType)
                nameParts.Insert(0, parentType.Identifier.Text);

            parent = parent.Parent;
        }

        return string.Join(".", nameParts);
    }

    private static bool ShouldSkipType(string fullTypeName, Dictionary<string, TypeInfo> allTypes)
    {
        var rootTypeName = ExtractRootType(fullTypeName);

        foreach (var skipNs in _config.SkipNamespaces)
        {
            if (rootTypeName.StartsWith(skipNs + ".", StringComparison.Ordinal) ||
                rootTypeName.StartsWith("global::" + skipNs + ".", StringComparison.Ordinal))
                return true;
        }

        var dotIndex = rootTypeName.LastIndexOf('.');
        var simpleTypeName = dotIndex >= 0 ? rootTypeName[(dotIndex + 1)..] : rootTypeName;

        if (_config.SkipTypes.Contains(simpleTypeName))
            return true;

        if (allTypes.TryGetValue(rootTypeName, out var typeInfo))
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var baseTypeSpan = baseType.TypeName.AsSpan();
                var genIdx = baseTypeSpan.IndexOf('<');
                if (genIdx >= 0)
                    baseTypeSpan = baseTypeSpan[..genIdx];

                var baseDotIdx = baseTypeSpan.LastIndexOf('.');
                var baseTypeName = baseDotIdx >= 0 ? baseTypeSpan[(baseDotIdx + 1)..].Trim() : baseTypeSpan.Trim();

                foreach (var excluded in _config.ExcludedBaseTypes)
                {
                    if (baseTypeName.StartsWith(excluded.AsSpan(), StringComparison.Ordinal))
                        return true;
                }
            }
        }

        return false;
    }

    private static void AddTypeAndDependencies(string typeName, HashSet<string> serializableTypes, AnalysisContext analysis)
    {
        if (ShouldSkipType(typeName, analysis.TypesByFullName))
            return;

        var rootTypeName = ExtractRootType(typeName);

        if (!analysis.TypesByFullName.TryGetValue(rootTypeName, out var typeInfo))
            return;

        var currentNs = typeInfo.Namespace;
        var genericParameterMap = BuildGenericParameterMap(typeName, typeInfo);

        if (typeInfo.GenericParameters.Count > 0 && genericParameterMap.Count != typeInfo.GenericParameters.Count)
            return;

        if (!serializableTypes.Add(typeName))
            return;

        var dotIndex = rootTypeName.LastIndexOf('.');
        var simpleTypeName = dotIndex >= 0 ? rootTypeName[(dotIndex + 1)..] : rootTypeName;
        var skipBaseTraversal = _config.RootTypeSkipNames.Contains(simpleTypeName);

        if (!skipBaseTraversal)
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var usings = analysis.AllUsingsByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyUsings;
                var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyAliases;
                var closedBaseType = ApplyGenericParameterMap(baseType.TypeName, genericParameterMap);
                    ProcessTypeExpression(closedBaseType, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
            }
        }

        foreach (var prop in typeInfo.Properties)
        {
            var usings = analysis.AllUsingsByFile.GetValueOrDefault(prop.FilePath) ?? _emptyUsings;
            var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(prop.FilePath) ?? _emptyAliases;
            var closedPropertyType = ApplyGenericParameterMap(prop.TypeName, genericParameterMap);
            ProcessTypeExpression(closedPropertyType, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback: true);
    }
    }

    private static Dictionary<string, string> BuildGenericParameterMap(string closedTypeName, TypeInfo typeInfo)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (typeInfo.GenericParameters.Count == 0)
            return map;

        var typeArguments = ExtractTypeArguments(closedTypeName);

        if (typeArguments.Count != typeInfo.GenericParameters.Count)
            return map;

        for (var i = 0; i < typeInfo.GenericParameters.Count; i++)
            map[typeInfo.GenericParameters[i]] = typeArguments[i];

        return map;
    }

    private static string ApplyGenericParameterMap(string typeExpression, Dictionary<string, string> genericParameterMap, HashSet<string>? visited = null)
    {
        if (genericParameterMap.Count == 0 || string.IsNullOrWhiteSpace(typeExpression))
            return typeExpression;

        var sb = new StringBuilder(typeExpression.Length);

        for (var i = 0; i < typeExpression.Length;)
        {
            if (char.IsLetter(typeExpression[i]) || typeExpression[i] == '_')
            {
                var start = i++;

                while (i < typeExpression.Length && (char.IsLetterOrDigit(typeExpression[i]) || typeExpression[i] == '_'))
                    i++;

                var token = typeExpression[start..i];
                var prev = start > 0 ? typeExpression[start - 1] : '\0';
                var next = i < typeExpression.Length ? typeExpression[i] : '\0';
                var isQualifiedToken = prev == '.' || next == '.';

                if (!isQualifiedToken && genericParameterMap.TryGetValue(token, out var replacement))
                {
                    visited ??= new HashSet<string>(StringComparer.Ordinal);

                    if (visited.Add(token))
                    {
                        sb.Append(ApplyGenericParameterMap(replacement, genericParameterMap, visited));
                        visited.Remove(token);
                    }
                    else
                        sb.Append(token);
                }
                else
                    sb.Append(token);

                continue;
            }

            sb.Append(typeExpression[i]);
            i++;
        }

        return sb.ToString();
    }

    private static void ProcessTypeExpression(string typeExpression,
                                              string currentNamespace,
                                              string currentTypeFullName,
                                              List<string> usings,
                                              Dictionary<string, string> typeAliases,
                                              HashSet<string> serializableTypes,
                                              AnalysisContext analysis,
                                              bool allowExternalFallback = false)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return;

        var fullResolvedType = ResolveFullTypeExpression(typeExpression, currentNamespace, currentTypeFullName, usings, typeAliases, analysis, allowExternalFallback);

        if (fullResolvedType != null)
        {
            var baseTypeWithoutArray = fullResolvedType;
            while (baseTypeWithoutArray.EndsWith("[]", StringComparison.Ordinal))
                baseTypeWithoutArray = baseTypeWithoutArray[..^2];

            var isBuiltInCollection = false;
            var genericIdx = baseTypeWithoutArray.IndexOf('<');

            if (genericIdx > 0)
            {
                var root = baseTypeWithoutArray[..genericIdx];
                var dotIdx = root.LastIndexOf('.');
                var simpleName = dotIdx >= 0 ? root[(dotIdx + 1)..] : root;

                if (_config.BuiltInCollectionTypes.Contains(simpleName))
                {
                    isBuiltInCollection = true;
                    serializableTypes.Add(baseTypeWithoutArray);
                }
            }

            if (!isBuiltInCollection)
                AddTypeAndDependencies(baseTypeWithoutArray, serializableTypes, analysis);

            if (fullResolvedType != baseTypeWithoutArray)
                serializableTypes.Add(fullResolvedType);

            const string ienumerablePrefix = "System.Collections.Generic.IEnumerable<";

            if (fullResolvedType.StartsWith(ienumerablePrefix, StringComparison.Ordinal))
            {
                var listType = string.Concat("System.Collections.Generic.List<", fullResolvedType.AsSpan(ienumerablePrefix.Length));
                serializableTypes.Add(listType);
            }
        }

        foreach (var arg in ExtractTypeArguments(typeExpression))
            ProcessTypeExpression(arg, currentNamespace, currentTypeFullName, usings, typeAliases, serializableTypes, analysis, allowExternalFallback);
    }

    private static string? ResolveFullTypeExpression(string typeExpression,
                                                     string currentNamespace,
                                                     string currentTypeFullName,
                                                     List<string> usings,
                                                     Dictionary<string, string> typeAliases,
                                                     AnalysisContext analysis,
                                                     bool allowExternalFallback = false)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return null;

        typeExpression = typeExpression.Trim();

        if (typeAliases.Count > 0)
            typeExpression = ExpandAliasInTypeName(typeExpression, typeAliases);

        if (typeExpression.EndsWith("?", StringComparison.Ordinal))
            typeExpression = typeExpression.TrimEnd('?');

        var arraySuffix = "";

        while (typeExpression.EndsWith("[]", StringComparison.Ordinal))
        {
            arraySuffix += "[]";
            typeExpression = typeExpression[..^2].TrimEnd();
        }

        var genericIndex = typeExpression.IndexOf('<');

        if (genericIndex > 0)
        {
            var rootName = typeExpression[..genericIndex];
            var resolvedRoot = ResolveTypeName(rootName, currentNamespace, currentTypeFullName, usings, typeAliases, analysis.TypesByFullName, analysis.SimpleNameToFullName);

            if (resolvedRoot == null && allowExternalFallback && IsExternalFallbackCandidate(rootName))
            {
                analysis.EnsureReferencedProjectsLoaded();
                resolvedRoot = ResolveTypeName(rootName, currentNamespace, currentTypeFullName, usings, typeAliases, analysis.TypesByFullName, analysis.SimpleNameToFullName);

                if (resolvedRoot == null)
                    resolvedRoot = analysis.TryLoadNuGetType(rootName, currentNamespace, usings);
            }

            if (resolvedRoot == null)
            {
                var dotIndex = rootName.LastIndexOf('.');
                var simpleTypeName = dotIndex >= 0 ? rootName[(dotIndex + 1)..] : rootName;

                if (_config.BuiltInCollectionTypes.Contains(simpleTypeName))
                {
                    var collectionNamespace = rootName.Contains('.')
                                                  ? rootName[..rootName.LastIndexOf('.')]
                                                  : "System.Collections.Generic";
                    resolvedRoot = $"{collectionNamespace}.{simpleTypeName}";
                }
            }

            if (resolvedRoot == null)
                return null;

            var args = ExtractTypeArguments(typeExpression);
            var resolvedArgs = new List<string>();

            foreach (var arg in args)
            {
                var resolvedArg = ResolveFullTypeExpression(arg, currentNamespace, currentTypeFullName, usings, typeAliases, analysis, allowExternalFallback);
                resolvedArgs.Add(resolvedArg ?? arg);
            }

            return $"{resolvedRoot}<{string.Join(", ", resolvedArgs)}>{arraySuffix}";
        }
        var resolved = ResolveTypeName(typeExpression, currentNamespace, currentTypeFullName, usings, typeAliases, analysis.TypesByFullName, analysis.SimpleNameToFullName);

        if (resolved == null && allowExternalFallback && IsExternalFallbackCandidate(typeExpression))
        {
            analysis.EnsureReferencedProjectsLoaded();
            resolved = ResolveTypeName(typeExpression, currentNamespace, currentTypeFullName, usings, typeAliases, analysis.TypesByFullName, analysis.SimpleNameToFullName);

            if (resolved == null)
                resolved = analysis.TryLoadNuGetType(typeExpression, currentNamespace, usings);
        }

        return resolved != null ? resolved + arraySuffix : null;
    }

    private static bool IsExternalFallbackCandidate(string typeExpression)
    {
        var rootType = ExtractRootType(typeExpression);

        if (rootType.StartsWith("global::", StringComparison.Ordinal))
            rootType = rootType[8..];

        foreach (var skipNs in _config.SkipNamespaces)
        {
            if (rootType.StartsWith(skipNs + ".", StringComparison.Ordinal))
                return false;
        }

        var dotIndex = rootType.LastIndexOf('.');
        var simpleName = dotIndex >= 0 && dotIndex < rootType.Length - 1
                             ? rootType[(dotIndex + 1)..]
                             : rootType;

        if (string.IsNullOrWhiteSpace(simpleName))
            return false;

        if (_config.BuiltInCollectionTypes.Contains(simpleName) || _primitiveTypeNames.Contains(simpleName) || _primitiveTypeNames.Contains(rootType))
            return false;

        return char.IsUpper(simpleName[0]);
    }

    private static string ExtractRootType(string typeName)
    {
        if (_rootTypeCache.TryGetValue(typeName, out var rootType))
            return rootType;

        var tn = typeName.TrimEnd('?');
        var genericIndex = tn.IndexOf('<');

        if (genericIndex > 0)
            tn = tn[..genericIndex];

        var arrayIndex = tn.IndexOf('[');
        if (arrayIndex > 0)
            tn = tn[..arrayIndex];

        rootType = tn.Trim();
        _rootTypeCache[typeName] = rootType;

        return rootType;
    }

    private static string GetContainingNamespace(SyntaxNode node)
    {
        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax nsDecl)
                return nsDecl.Name.ToString();

            parent = parent.Parent;
        }

        return "";
    }

    private static string ExpandAliasInTypeName(string typeName, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.StartsWith("global::", StringComparison.Ordinal))
            return typeName;

        var stop = typeName.Length;
        var dot = typeName.IndexOf('.');

        if (dot >= 0)
            stop = Math.Min(stop, dot);

        var lt = typeName.IndexOf('<');

        if (lt >= 0)
            stop = Math.Min(stop, lt);

        var br = typeName.IndexOf('[');

        if (br >= 0)
            stop = Math.Min(stop, br);

        var root = stop == typeName.Length ? typeName : typeName[..stop];

        if (!aliases.TryGetValue(root, out var target))
            return typeName;

        var rest = stop == typeName.Length
                       ? string.Empty
                       : typeName[stop..];

        return target + rest;
    }

    internal static TypeInfo MergeTypeInfos(TypeInfo existing, TypeInfo incoming)
    {
        if (ReferenceEquals(existing, incoming))
            return existing;

        var mergedProps = existing.Properties
                                  .Concat(incoming.Properties)
                                  .GroupBy(p => p.Name, StringComparer.Ordinal)
                                  .Select(g => g.First())
                                  .ToList();

        var mergedBaseTypes = existing.BaseTypes
                                      .Concat(incoming.BaseTypes)
                                      .GroupBy(b => b.TypeName, StringComparer.Ordinal)
                                      .Select(g => g.First())
                                      .ToList();

        return existing with
        {
            Properties = mergedProps,
            BaseTypes = mergedBaseTypes,
            GenericParameters = existing.GenericParameters.Count >= incoming.GenericParameters.Count
                                    ? existing.GenericParameters
                                    : incoming.GenericParameters
        };
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

    private static ReferencedProjectData LoadReferencedProjectData(SourceFileSet referencedProjectSources)
    {
        if (referencedProjectSources.Files.Count == 0)
            return new(new(StringComparer.Ordinal), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), referencedProjectSources.Hash);

        var parseResults = ParseAllFiles(referencedProjectSources.Files);
        var (_, typeDeclarations, allUsingsByFile, allTypeAliasesByFile) = AggregateResults(parseResults);

        return new(typeDeclarations, allUsingsByFile, allTypeAliasesByFile, referencedProjectSources.Hash);
    }

    private static NuGetPackageAssemblySet GetNuGetPackageCompileAssembliesWithHash(string projectPath,
                                                                                     string? assetsFilePath,
                                                                                     string? targetFramework,
                                                                                     string? runtimeIdentifier,
                                                                                     string? targetingPackRoot)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var resolvedAssetsFilePath = ResolveAssetsFilePath(projectDir, assetsFilePath);
        var resolvedTargetFramework = string.IsNullOrWhiteSpace(targetFramework) ? GetTargetFramework(projectPath) : targetFramework;

        if (resolvedAssetsFilePath == null || !File.Exists(resolvedAssetsFilePath))
            return new([], [], string.Empty);

        var packageAssemblies = ReadNuGetPackageCompileAssemblies(resolvedAssetsFilePath, resolvedTargetFramework, runtimeIdentifier);
        var assemblyPaths = packageAssemblies.Select(a => a.AssemblyPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var referenceAssemblies = GetTargetFrameworkReferenceAssemblies(resolvedTargetFramework, targetingPackRoot);
        var hashInputs = new List<string> { resolvedAssetsFilePath };
        hashInputs.AddRange(referenceAssemblies);

        return new(packageAssemblies, referenceAssemblies, ComputeContentHash(assemblyPaths, hashInputs));
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
            return [];

        var targetKey = SelectAssetsTargetKey(targetsElement, targetFramework, runtimeIdentifier);

        if (targetKey == null || !targetsElement.TryGetProperty(targetKey, out var targetElement) || targetElement.ValueKind != JsonValueKind.Object)
            return [];

        var packageFolders = ReadPackageFolders(document.RootElement);
        if (packageFolders.Count == 0)
            return [];

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
                    continue;

                packageAssemblies.Add(new(libraryKey, assemblyPath));
            }
        }

        return packageAssemblies;
    }

    private static string? SelectAssetsTargetKey(JsonElement targetsElement, string? targetFramework, string? runtimeIdentifier)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(targetFramework) && !string.IsNullOrWhiteSpace(runtimeIdentifier))
            candidates.Add($"{targetFramework}/{runtimeIdentifier}");

        if (!string.IsNullOrWhiteSpace(targetFramework))
            candidates.Add(targetFramework);

        foreach (var candidate in candidates)
        {
            if (targetsElement.TryGetProperty(candidate, out _))
                return candidate;
        }

        foreach (var target in targetsElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(targetFramework) || target.Name.StartsWith(targetFramework, StringComparison.OrdinalIgnoreCase))
                return target.Name;
        }

        return targetsElement.EnumerateObject().FirstOrDefault().Name;
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
    {
        return libraryValue.TryGetProperty("type", out var typeElement) &&
               string.Equals(typeElement.GetString(), "package", StringComparison.OrdinalIgnoreCase);
    }

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
                           : targetingPackRoot!;

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

        return refAssemblyPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddReferenceAssemblies(List<string> paths, string packRoot, string packName, string targetFramework)
    {
        var packDir = Path.Combine(packRoot, packName);

        if (!Directory.Exists(packDir))
            return;

        var latestVersionDir = Directory.EnumerateDirectories(packDir).OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        if (latestVersionDir == null)
            return;

        var refDir = Path.Combine(latestVersionDir, "ref", targetFramework);

        if (!Directory.Exists(refDir))
            return;

        paths.AddRange(Directory.EnumerateFiles(refDir, "*.dll", SearchOption.TopDirectoryOnly));
    }

    private static NuGetPackageTypeLoader LoadNuGetPackageTypeLoader(NuGetPackageAssemblySet packageAssemblies)
        => NuGetPackageTypeLoader.Create(packageAssemblies.PackageAssemblies, packageAssemblies.ReferenceAssemblies, packageAssemblies.Hash);

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

    private static string GenerateSerializerContext(string rootNamespace, HashSet<string> types)
    {
        var b = new StringBuilder();

        b.l("// <auto-generated />");
        b.l("// Generated by FastEndpoints.Generator.Cli");
        b.l($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        b.l("");
        b.l("#pragma warning disable");
        b.l("#nullable enable");
        b.l("");
        b.l("using System.Text.Json.Serialization;");
        b.l("");
        b.l($"namespace {rootNamespace};");
        b.l("");
        b.l("/// <summary>");
        b.l($"/// STJ serializer context for {rootNamespace} endpoints.");
        b.l("/// </summary>");

        var usedTypeInfoNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types.OrderBy(t => t))
        {
            var globalStrippedType = type.StartsWith("global::") ? type.TrimStart("global::") : type;
            var typeInfoName = MakeTypeInfoPropertyName(type);
            if (!usedTypeInfoNames.Add(typeInfoName))
                typeInfoName = typeInfoName + "_" + ComputeStableShortHash(typeInfoName);

            b.l($"[JsonSerializable(typeof({globalStrippedType}), TypeInfoPropertyName = \"{typeInfoName}\")]");
        }

        b.l("internal partial class GeneratedSerializerContext : JsonSerializerContext;");

        return b.ToString();
    }

    private static string MakeTypeInfoPropertyName(string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
            return "TI_Unknown";

        var dotIndex = fullTypeName.LastIndexOf('.');
        var simpleName = dotIndex >= 0 ? fullTypeName[(dotIndex + 1)..] : fullTypeName;
        simpleName = simpleName.Sanitize();

        var hash = ComputeStableShortHash(fullTypeName);

        return $"TI_{simpleName}_{hash}";
    }

    private static string ComputeStableShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes, 0, 4);
    }

    private static string GenerateExtensionMethod(string rootNamespace)
    {
        var sb = new StringBuilder();

        sb.l("// <auto-generated />");
        sb.l("// Generated by FastEndpoints.Generator.Cli");
        sb.l("");
        sb.l("#pragma warning disable");
        sb.l("#nullable enable");
        sb.l("");
        sb.l("using System.Text.Json;");
        sb.l("");
        sb.l($"namespace {rootNamespace};");
        sb.l("");
        sb.l("/// <summary>");
        sb.l("/// Extension methods for adding generated serializer contexts.");
        sb.l("/// </summary>");
        sb.l("internal static class FastEndpointsSerializerExtensions");
        sb.l("{");
        sb.l("    /// <summary>");
        sb.l("    /// Adds the generated JSON serializer context to the JSON serializer options.");
        sb.l("    /// </summary>");
        sb.l($"   internal static JsonSerializerOptions AddSerializerContextsFrom{rootNamespace}(this JsonSerializerOptions options)");
        sb.l("    {");
        sb.l("        var context = new GeneratedSerializerContext(new(options));");
        sb.l("        options.TypeInfoResolverChain.Insert(0, context);");
        sb.l("");
        sb.l("        return options;");
        sb.l("    }");
        sb.l("}");

        return sb.ToString();
    }

    [GeneratedRegex("<(.+)>")]
    private static partial Regex TypeArgMatcherRegex();
}

record AnalysisContext(Dictionary<string, TypeInfo> TypesByFullName,
                       Dictionary<string, List<string>> AllUsingsByFile,
                       Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile)
{
    private Func<ReferencedProjectData>? _referencedProjectLoader;
    private Func<NuGetPackageTypeLoader>? _nuGetPackageLoader;
    private NuGetPackageTypeLoader? _loadedNuGetPackageTypes;

    public Dictionary<string, string> SimpleNameToFullName { get; } = new(StringComparer.Ordinal);
    public bool ReferencedProjectsInspected { get; private set; }
    public string? ReferencedProjectsHash { get; private set; }
    public bool NuGetPackagesInspected { get; private set; }
    public string? NuGetPackagesHash { get; private set; }

    public static AnalysisContext Create(Dictionary<string, TypeInfo> types,
                                         Dictionary<string, List<string>> usings,
                                         Dictionary<string, Dictionary<string, string>> aliases)
    {
        var ctx = new AnalysisContext(types, usings, aliases);

        foreach (var fullName in types.Keys)
        {
            var dotIndex = fullName.LastIndexOf('.');
            var simpleName = dotIndex >= 0 ? fullName[(dotIndex + 1)..] : fullName;

            if (!ctx.SimpleNameToFullName.ContainsKey(simpleName))
                ctx.SimpleNameToFullName[simpleName] = fullName;
        }

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

            var dotIndex = fullName.LastIndexOf('.');
            var simpleName = dotIndex >= 0 ? fullName[(dotIndex + 1)..] : fullName;

            if (!SimpleNameToFullName.ContainsKey(simpleName))
                SimpleNameToFullName[simpleName] = fullName;
        }

        foreach (var (filePath, usings) in referencedProjectData.AllUsingsByFile)
            AllUsingsByFile[filePath] = usings;

        foreach (var (filePath, aliases) in referencedProjectData.AllTypeAliasesByFile)
            AllTypeAliasesByFile[filePath] = aliases;
    }

    public string? TryLoadNuGetType(string typeExpression, string currentNamespace, List<string> usings)
    {
        var packageLoader = EnsureNuGetPackageLoader();

        if (packageLoader == null)
            return null;

        if (!packageLoader.TryResolveAndLoadType(typeExpression, currentNamespace, usings, out var fullName, out var typeInfo))
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

        var dotIndex = typeInfo.FullName.LastIndexOf('.');
        var simpleName = dotIndex >= 0 ? typeInfo.FullName[(dotIndex + 1)..] : typeInfo.FullName;

        if (!SimpleNameToFullName.ContainsKey(simpleName))
            SimpleNameToFullName[simpleName] = typeInfo.FullName;
    }
}

sealed class NuGetPackageTypeLoader
{
    private static readonly SymbolDisplayFormat _qualifiedTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandNullable);

    private static readonly SymbolDisplayFormat _namespaceFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private readonly Dictionary<string, INamedTypeSymbol> _typesByFullName;
    private readonly Dictionary<string, string> _typeNamespaces;
    private readonly Dictionary<string, List<string>> _typesBySimpleName;
    private readonly Dictionary<string, TypeInfo> _typeInfoCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _resolutionCache = new(StringComparer.Ordinal);

    private NuGetPackageTypeLoader(Dictionary<string, INamedTypeSymbol> typesByFullName,
                                   Dictionary<string, string> typeNamespaces,
                                   Dictionary<string, List<string>> typesBySimpleName,
                                   string hash)
    {
        _typesByFullName = typesByFullName;
        _typeNamespaces = typeNamespaces;
        _typesBySimpleName = typesBySimpleName;
        Hash = hash;
    }

    public string Hash { get; }

    public static NuGetPackageTypeLoader Create(List<NuGetPackageAssemblyInfo> packageAssemblies, List<string> referenceAssemblies, string hash)
    {
        var typesByFullName = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var typeNamespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        var typesBySimpleName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (packageAssemblies.Count == 0)
            return new(typesByFullName, typeNamespaces, typesBySimpleName, hash);

        var metadataReferencePaths = referenceAssemblies.Concat(packageAssemblies.Select(a => a.AssemblyPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var metadataReferences = metadataReferencePaths.Select(path => MetadataReference.CreateFromFile(path)).ToList();
        var compilation = CSharpCompilation.Create("FastEndpoints.Generator.Cli.NuGetMetadata", references: metadataReferences);
        var referencesByPath = metadataReferencePaths.Zip(metadataReferences, (path, reference) => new KeyValuePair<string, MetadataReference>(path, reference))
                                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in packageAssemblies.Select(a => a.AssemblyPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!referencesByPath.TryGetValue(assemblyPath, out var reference))
                continue;

            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                continue;

            CollectTypes(assemblySymbol.GlobalNamespace, typesByFullName, typeNamespaces, typesBySimpleName);
        }

        return new(typesByFullName, typeNamespaces, typesBySimpleName, hash);
    }

    public bool TryResolveAndLoadType(string typeExpression,
                                      string currentNamespace,
                                      List<string> usings,
                                      out string fullName,
                                      out TypeInfo typeInfo)
    {
        fullName = string.Empty;
        typeInfo = default!;

        var resolvedFullName = ResolveFullName(typeExpression, currentNamespace, usings);

        if (resolvedFullName == null || !_typesByFullName.TryGetValue(resolvedFullName, out var symbol))
            return false;

        fullName = resolvedFullName;

        if (!_typeInfoCache.TryGetValue(fullName, out var cachedTypeInfo))
        {
            cachedTypeInfo = CreateTypeInfo(symbol);
            _typeInfoCache[fullName] = cachedTypeInfo;
        }

        typeInfo = cachedTypeInfo;

        return true;
    }

    private string? ResolveFullName(string typeExpression, string currentNamespace, List<string> usings)
    {
        var lookupName = NormalizeLookupName(typeExpression);
        var cacheKey = BuildResolutionKey(lookupName, currentNamespace, usings);

        if (_resolutionCache.TryGetValue(cacheKey, out var cachedFullName))
            return cachedFullName;

        var resolvedFullName = ResolveFullNameCore(lookupName, currentNamespace, usings);
        _resolutionCache[cacheKey] = resolvedFullName;

        return resolvedFullName;
    }

    private string? ResolveFullNameCore(string lookupName, string currentNamespace, List<string> usings)
    {
        if (_typesByFullName.ContainsKey(lookupName))
            return lookupName;

        if (!string.IsNullOrWhiteSpace(currentNamespace))
        {
            var currentNamespaceCandidate = $"{currentNamespace}.{lookupName}";

            if (_typesByFullName.ContainsKey(currentNamespaceCandidate))
                return currentNamespaceCandidate;
        }

        foreach (var usingNamespace in usings)
        {
            var usingCandidate = $"{usingNamespace}.{lookupName}";

            if (_typesByFullName.ContainsKey(usingCandidate))
                return usingCandidate;
        }

        var dotIndex = lookupName.LastIndexOf('.');
        var simpleName = dotIndex >= 0 ? lookupName[(dotIndex + 1)..] : lookupName;

        if (!_typesBySimpleName.TryGetValue(simpleName, out var candidates) || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var contextualCandidates = candidates.Where(c => MatchesContext(c, currentNamespace, usings)).ToList();

        if (contextualCandidates.Count == 1)
            return contextualCandidates[0];

        return null;
    }

    private bool MatchesContext(string fullName, string currentNamespace, List<string> usings)
    {
        if (!_typeNamespaces.TryGetValue(fullName, out var typeNamespace))
            return false;

        return string.Equals(typeNamespace, currentNamespace, StringComparison.Ordinal) ||
               usings.Contains(typeNamespace, StringComparer.Ordinal);
    }

    private static string BuildResolutionKey(string lookupName, string currentNamespace, List<string> usings)
        => string.Concat(lookupName, "|", currentNamespace, "|", string.Join(";", usings));

    private static string NormalizeLookupName(string typeExpression)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return string.Empty;

        var normalizedType = typeExpression.Trim().TrimEnd('?');

        if (normalizedType.StartsWith("global::", StringComparison.Ordinal))
            normalizedType = normalizedType[8..];

        var genericIndex = normalizedType.IndexOf('<');

        if (genericIndex >= 0)
            normalizedType = normalizedType[..genericIndex];

        var arrayIndex = normalizedType.IndexOf('[');

        if (arrayIndex >= 0)
            normalizedType = normalizedType[..arrayIndex];

        return normalizedType.Trim();
    }

    private static void CollectTypes(INamespaceSymbol namespaceSymbol,
                                     Dictionary<string, INamedTypeSymbol> typesByFullName,
                                     Dictionary<string, string> typeNamespaces,
                                     Dictionary<string, List<string>> typesBySimpleName)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
            CollectTypes(typeSymbol, typesByFullName, typeNamespaces, typesBySimpleName);

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            CollectTypes(childNamespace, typesByFullName, typeNamespaces, typesBySimpleName);
    }

    private static void CollectTypes(INamedTypeSymbol typeSymbol,
                                     Dictionary<string, INamedTypeSymbol> typesByFullName,
                                     Dictionary<string, string> typeNamespaces,
                                     Dictionary<string, List<string>> typesBySimpleName)
    {
        if (IsPubliclyAccessible(typeSymbol))
        {
            var fullName = GetTypeDefinitionName(typeSymbol);

            if (!typesByFullName.ContainsKey(fullName))
            {
                typesByFullName[fullName] = typeSymbol;
                typeNamespaces[fullName] = GetNamespace(typeSymbol);

                if (!typesBySimpleName.TryGetValue(typeSymbol.Name, out var matches))
                {
                    matches = [];
                    typesBySimpleName[typeSymbol.Name] = matches;
                }

                matches.Add(fullName);
            }
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
            CollectTypes(nestedType, typesByFullName, typeNamespaces, typesBySimpleName);
    }

    private static bool IsPubliclyAccessible(INamedTypeSymbol typeSymbol)
    {
        if (!typeSymbol.CanBeReferencedByName || typeSymbol.DeclaredAccessibility != Accessibility.Public)
            return false;

        for (var containingType = typeSymbol.ContainingType; containingType != null; containingType = containingType.ContainingType)
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
                return false;
        }

        return true;
    }

    private static TypeInfo CreateTypeInfo(INamedTypeSymbol typeSymbol)
    {
        typeSymbol = typeSymbol.OriginalDefinition;

        var fullName = GetTypeDefinitionName(typeSymbol);
        var filePath = fullName;
        var properties = typeSymbol.GetMembers()
                                  .OfType<IPropertySymbol>()
                                  .Where(p => !p.IsStatic && !p.IsIndexer && IsSerializableProperty(p))
                                  .Select(p => new PropertyInfo(p.Name, GetDisplayTypeName(p.Type), filePath))
                                  .ToList();

        var baseTypes = new List<TypeRef>();

        if (typeSymbol.TypeKind == TypeKind.Class && typeSymbol.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            baseTypes.Add(new(GetDisplayTypeName(baseType), filePath));

        var genericParameters = typeSymbol.TypeParameters.Select(p => p.Name).ToList();

        return new(fullName, GetNamespace(typeSymbol), typeSymbol.Name, filePath, properties, baseTypes, genericParameters);
    }

    private static bool IsSerializableProperty(IPropertySymbol propertySymbol)
        => propertySymbol.DeclaredAccessibility == Accessibility.Public ||
           propertySymbol.GetMethod?.DeclaredAccessibility == Accessibility.Public ||
           propertySymbol.SetMethod?.DeclaredAccessibility == Accessibility.Public;

    private static string GetNamespace(INamedTypeSymbol typeSymbol)
        => typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.ToDisplayString(_namespaceFormat);

    private static string GetTypeDefinitionName(INamedTypeSymbol typeSymbol)
    {
        var nameParts = new Stack<string>();

        for (var current = typeSymbol.OriginalDefinition; current != null; current = current.ContainingType)
            nameParts.Push(current.Name);

        var namespaceName = GetNamespace(typeSymbol);
        var typeName = string.Join('.', nameParts);

        return string.IsNullOrWhiteSpace(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
    }

    private static string GetDisplayTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol switch
        {
            IArrayTypeSymbol arrayType => $"{GetDisplayTypeName(arrayType.ElementType)}[]",
            INamedTypeSymbol namedType when namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && namedType.TypeArguments.Length == 1
                => GetDisplayTypeName(namedType.TypeArguments[0]),
            INamedTypeSymbol namedType => GetNamedTypeDisplayName(namedType),
            ITypeParameterSymbol typeParameter => typeParameter.Name,
            _ => typeSymbol.ToDisplayString(_qualifiedTypeFormat)
        };
    }

    private static string GetNamedTypeDisplayName(INamedTypeSymbol typeSymbol)
    {
        var rootTypeName = GetTypeDefinitionName(typeSymbol.OriginalDefinition);

        if (!typeSymbol.IsGenericType || typeSymbol.TypeArguments.Length == 0)
            return rootTypeName;

        var typeArguments = typeSymbol.TypeArguments.Select(GetDisplayTypeName);

        return $"{rootTypeName}<{string.Join(", ", typeArguments)}>";
    }
}

record SourceFileSet(List<string> Files, string Hash);

record NuGetPackageAssemblySet(List<NuGetPackageAssemblyInfo> PackageAssemblies,
                               List<string> ReferenceAssemblies,
                               string Hash);

record NuGetPackageAssemblyInfo(string LibraryKey, string AssemblyPath);

record ReferencedProjectData(Dictionary<string, TypeInfo> TypesByFullName,
                             Dictionary<string, List<string>> AllUsingsByFile,
                             Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile,
                             string Hash);

record GeneratorCacheState(string Version,
                           string MainProjectHash,
                           bool ReferencedProjectsInspected,
                           string ReferencedProjectsHash,
                           bool NuGetPackagesInspected,
                           string NuGetPackagesHash);

record FileParseResult(string FilePath,
                       SyntaxTree Tree,
                       List<string> FileUsings,
                       List<string> GlobalUsings,
                       Dictionary<string, string> FileTypeAliases,
                       Dictionary<string, string> GlobalTypeAliases,
                       Dictionary<string, TypeInfo> Types);

record TypeInfo(string FullName,
                string Namespace,
                string Name,
                string FilePath,
                List<PropertyInfo> Properties,
                List<TypeRef> BaseTypes,
                List<string> GenericParameters);

record PropertyInfo(string Name, string TypeName, string FilePath);

record TypeRef(string TypeName, string FilePath);
