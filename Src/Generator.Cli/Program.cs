using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
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

    private const string CacheFileName = ".fastendpoints-generator-cache";

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
            "IEnumerable",
            "List",
            "ICollection",
            "IList",
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

        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Error: Project file not found: {projectPath}");

            return 1;
        }

        try
        {
            return RunGenerator(projectPath, forceRegenerate, customOutputPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");

            return 1;
        }
    }

    private static int RunGenerator(string projectPath, bool forceRegenerate, string? customOutputPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

        Console.WriteLine($"Analyzing project: {projectName}");

        var (csFiles, currentHash) = GetSourceFilesWithHash(projectDir);

        if (csFiles.Count == 0)
        {
            Console.WriteLine("No C# source files found.");

            return 0;
        }

        Console.WriteLine($"Found {csFiles.Count} source files.");

        var outputDir = GetGeneratorOutputPath(projectDir, customOutputPath);
        var cacheFilePath = Path.Combine(outputDir, CacheFileName);

        if (!forceRegenerate && IsUpToDate(cacheFilePath, currentHash, outputDir))
        {
            Console.WriteLine("Generated files are up-to-date. Skipping generation.");

            return 0;
        }

        var parseResults = ParseAllFiles(csFiles);
        var (syntaxTrees, typeDeclarations, allUsingsByFile, allTypeAliasesByFile) = AggregateResults(parseResults);
        var analysis = AnalysisContext.Create(typeDeclarations, allUsingsByFile, allTypeAliasesByFile);

        Console.WriteLine($"Found {typeDeclarations.Count} type declarations.");

        var (serializableTypes, endpointCount) = AnalyzeEndpoints(syntaxTrees, analysis);

        Console.WriteLine($"Found {endpointCount} endpoints with {serializableTypes.Count} serializable types.");

        if (serializableTypes.Count == 0)
        {
            Console.WriteLine("No serializable types found in endpoints.");

            return 0;
        }

        GenerateOutput(outputDir, projectPath, projectName, serializableTypes, currentHash, cacheFilePath);

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

    private static (List<(SyntaxTree Tree, string FilePath)> SyntaxTrees,
        Dictionary<string, TypeInfo> TypeDeclarations,
        Dictionary<string, List<string>> AllUsingsByFile,
        Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile)
        AggregateResults(ConcurrentBag<FileParseResult> parseResults)
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
                    ProcessTypeExpression(typeArg, currentNs, className, allUsings, typeAliases, serializableTypes, analysis);
            }
        }

        return (serializableTypes, endpointCount);
    }

    private static void GenerateOutput(string outputDir, string projectPath, string projectName, HashSet<string> serializableTypes, string currentHash, string cacheFilePath)
    {
        Directory.CreateDirectory(outputDir);

        var rootNamespace = GetRootNamespace(projectPath) ?? projectName.Sanitize();
        var contextCode = GenerateSerializerContext(rootNamespace, serializableTypes);
        var contextPath = Path.Combine(outputDir, "SerializerContexts.g.cs");
        File.WriteAllText(contextPath, contextCode, Encoding.UTF8);

        var extensionCode = GenerateExtensionMethod(rootNamespace);
        var extensionPath = Path.Combine(outputDir, "SerializerContextExtensions.g.cs");
        File.WriteAllText(extensionPath, extensionCode, Encoding.UTF8);

        File.WriteAllText(cacheFilePath, currentHash);

        Console.WriteLine("Generated files:");
        Console.WriteLine($"  {contextPath}");
        Console.WriteLine($"  {extensionPath}");
    }

    private static (List<string> Files, string Hash) GetSourceFilesWithHash(string projectDir)
    {
        var separator = Path.DirectorySeparatorChar;
        var binSep = $"{separator}bin{separator}";
        var objSep = $"{separator}obj{separator}";

        var files = new List<string>();
        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.Contains(binSep) && !file.Contains(objSep) && !file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(file);
                writer.WriteLine(file);
                writer.WriteLine(File.GetLastWriteTimeUtc(file).Ticks);
            }
        }

        writer.Flush();
        stream.Position = 0;
        var hash = Convert.ToHexString(sha256.ComputeHash(stream));

        return (files, hash);
    }

    private static bool IsUpToDate(string cacheFilePath, string currentHash, string outputDir)
    {
        if (!File.Exists(cacheFilePath))
            return false;

        var contextPath = Path.Combine(outputDir, "SerializerContexts.g.cs");
        var extensionPath = Path.Combine(outputDir, "SerializerContextExtensions.g.cs");

        if (!File.Exists(contextPath) || !File.Exists(extensionPath))
            return false;

        var cachedHash = File.ReadAllText(cacheFilePath).Trim();

        return cachedHash == currentHash;
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

                var typeInfo = new TypeInfo(fullName, ns, typeDecl.Identifier.Text, filePath, properties, baseTypes);
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

            var baseName = baseType.Split('<')[0].Split('.').Last().Trim();

            if (_config.EndpointBaseTypePatterns.Any(p => baseName.StartsWith(p, StringComparison.Ordinal)))
                return baseType;
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
        foreach (var skipNs in _config.SkipNamespaces)
        {
            if (fullTypeName.StartsWith(skipNs + ".", StringComparison.Ordinal) ||
                fullTypeName.StartsWith("global::" + skipNs + ".", StringComparison.Ordinal))
                return true;
        }

        var simpleTypeName = fullTypeName.Split('.').Last();

        if (_config.SkipTypes.Contains(simpleTypeName))
            return true;

        if (allTypes.TryGetValue(fullTypeName, out var typeInfo))
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var baseTypeName = baseType.TypeName.Split('<')[0].Split('.').Last().Trim();

                if (_config.ExcludedBaseTypes.Any(e => baseTypeName.StartsWith(e, StringComparison.Ordinal)))
                    return true;
            }
        }

        return false;
    }

    private static void AddTypeAndDependencies(string typeName, HashSet<string> serializableTypes, AnalysisContext analysis)
    {
        if (ShouldSkipType(typeName, analysis.TypesByFullName))
            return;

        if (!serializableTypes.Add(typeName))
            return;

        if (!analysis.TypesByFullName.TryGetValue(typeName, out var typeInfo))
            return;

        var currentNs = typeInfo.Namespace;
        var simpleTypeName = typeName.Split('.').Last();
        var skipBaseTraversal = _config.RootTypeSkipNames.Contains(simpleTypeName);

        if (!skipBaseTraversal)
        {
            foreach (var baseType in typeInfo.BaseTypes)
            {
                var usings = analysis.AllUsingsByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyUsings;
                var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(baseType.FilePath) ?? _emptyAliases;
                ProcessTypeExpression(baseType.TypeName, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis);
            }
        }

        foreach (var prop in typeInfo.Properties)
        {
            var usings = analysis.AllUsingsByFile.GetValueOrDefault(prop.FilePath) ?? _emptyUsings;
            var typeAliases = analysis.AllTypeAliasesByFile.GetValueOrDefault(prop.FilePath) ?? _emptyAliases;
            ProcessTypeExpression(prop.TypeName, currentNs, typeInfo.FullName, usings, typeAliases, serializableTypes, analysis);
        }
    }

    private static void ProcessTypeExpression(string typeExpression,
                                              string currentNamespace,
                                              string currentTypeFullName,
                                              List<string> usings,
                                              Dictionary<string, string> typeAliases,
                                              HashSet<string> serializableTypes,
                                              AnalysisContext analysis)
    {
        if (string.IsNullOrWhiteSpace(typeExpression))
            return;

        typeExpression = typeExpression.Trim();

        if (typeAliases.Count > 0)
            typeExpression = ExpandAliasInTypeName(typeExpression, typeAliases);

        if (typeExpression.EndsWith("?", StringComparison.Ordinal))
            typeExpression = typeExpression.TrimEnd('?');

        // check if this is a collection type and register it
        var collectionType = TryResolveCollectionType(
            typeExpression,
            currentNamespace,
            currentTypeFullName,
            usings,
            typeAliases,
            analysis.TypesByFullName,
            analysis.SimpleNameToFullName);

        if (collectionType != null)
        {
            serializableTypes.Add(collectionType);

            // for IEnumerable<T>, also register List<T> since it's the most common concrete implementation
            const string ienumerablePrefix = "System.Collections.Generic.IEnumerable<";

            if (collectionType.StartsWith(ienumerablePrefix, StringComparison.Ordinal))
            {
                var listType = string.Concat("System.Collections.Generic.List<", collectionType.AsSpan(ienumerablePrefix.Length));
                serializableTypes.Add(listType);
            }
        }

        var resolved = ResolveTypeName(typeExpression, currentNamespace, currentTypeFullName, usings, typeAliases, analysis.TypesByFullName, analysis.SimpleNameToFullName);

        if (resolved != null)
            AddTypeAndDependencies(resolved, serializableTypes, analysis);

        foreach (var arg in ExtractTypeArguments(typeExpression))
            ProcessTypeExpression(arg, currentNamespace, currentTypeFullName, usings, typeAliases, serializableTypes, analysis);
    }

    private static string? TryResolveCollectionType(string typeExpression,
                                                    string currentNamespace,
                                                    string currentTypeFullName,
                                                    List<string> usings,
                                                    Dictionary<string, string> typeAliases,
                                                    Dictionary<string, TypeInfo> types,
                                                    Dictionary<string, string>? simpleNameCache = null)
    {
        if (typeExpression.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = typeExpression[..^2];
            var resolvedElement = ResolveTypeName(elementType, currentNamespace, currentTypeFullName, usings, typeAliases, types, simpleNameCache);

            if (resolvedElement != null)
                return resolvedElement + "[]";
        }

        var genericIndex = typeExpression.IndexOf('<');

        if (genericIndex > 0)
        {
            var typeName = typeExpression[..genericIndex];
            var simpleTypeName = typeName.Split('.').Last();

            if (_config.BuiltInCollectionTypes.Contains(simpleTypeName))
            {
                var args = ExtractTypeArguments(typeExpression);

                if (args.Count > 0)
                {
                    var resolvedArgs = new List<string>();

                    foreach (var arg in args)
                    {
                        var resolvedArg = ResolveTypeName(arg, currentNamespace, currentTypeFullName, usings, typeAliases, types, simpleNameCache);
                        resolvedArgs.Add(resolvedArg ?? arg);
                    }

                    var collectionNamespace = typeName.Contains('.')
                                                  ? typeName[..typeName.LastIndexOf('.')]
                                                  : "System.Collections.Generic";

                    return $"{collectionNamespace}.{simpleTypeName}<{string.Join(", ", resolvedArgs)}>";
                }
            }
        }

        return null;
    }

    private static string ExtractRootType(string typeName)
    {
        typeName = typeName.TrimEnd('?');
        var genericIndex = typeName.IndexOf('<');

        if (genericIndex > 0)
            typeName = typeName[..genericIndex];

        var arrayIndex = typeName.IndexOf('[');
        if (arrayIndex > 0)
            typeName = typeName[..arrayIndex];

        return typeName.Trim();
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

    private static TypeInfo MergeTypeInfos(TypeInfo existing, TypeInfo incoming)
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
            BaseTypes = mergedBaseTypes
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

        var simpleName = (fullTypeName.Split('.').LastOrDefault() ?? fullTypeName).Sanitize();
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
    public Dictionary<string, string> SimpleNameToFullName { get; } = new(StringComparer.Ordinal);

    public static AnalysisContext Create(Dictionary<string, TypeInfo> types,
                                         Dictionary<string, List<string>> usings,
                                         Dictionary<string, Dictionary<string, string>> aliases)
    {
        var ctx = new AnalysisContext(types, usings, aliases);

        foreach (var fullName in types.Keys)
        {
            var simpleName = fullName.Split('.').Last();
            if (!ctx.SimpleNameToFullName.ContainsKey(simpleName))
                ctx.SimpleNameToFullName[simpleName] = fullName;
        }

        return ctx;
    }
}

record FileParseResult(string FilePath,
                       SyntaxTree Tree,
                       List<string> FileUsings,
                       List<string> GlobalUsings,
                       Dictionary<string, string> FileTypeAliases,
                       Dictionary<string, string> GlobalTypeAliases,
                       Dictionary<string, TypeInfo> Types);

record TypeInfo(string FullName, string Namespace, string Name, string FilePath, List<PropertyInfo> Properties, List<TypeRef> BaseTypes);

record PropertyInfo(string Name, string TypeName, string FilePath);

record TypeRef(string TypeName, string FilePath);