using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static ConcurrentBag<FileParseResult> ParseSourceFiles(List<string> sourceFiles)
    {
        var parseResults = new ConcurrentBag<FileParseResult>();

        Parallel.ForEach(
            sourceFiles,
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
        Dictionary<string, Dictionary<string, string>> AllTypeAliasesByFile) BuildAnalysisInputs(ConcurrentBag<FileParseResult> parseResults)
    {
        var globalUsings = new HashSet<string>(StringComparer.Ordinal);
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
                globalUsings.Add(globalUsing);

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

        var allNodes = root.DescendantNodes();

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

                var ns = GetContainingNamespace(typeDecl);

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

    private static string GetFullTypeName(TypeDeclarationSyntax typeDecl)
    {
        var nameParts = new List<string> { GetTypeNameWithArity(typeDecl) };
        var parent = typeDecl.Parent;

        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax nsDecl)
                nameParts.Insert(0, nsDecl.Name.ToString());
            else if (parent is TypeDeclarationSyntax parentType)
                nameParts.Insert(0, GetTypeNameWithArity(parentType));

            parent = parent.Parent;
        }

        return string.Join(".", nameParts);
    }

    private static string GetTypeNameWithArity(TypeDeclarationSyntax typeDecl)
    {
        var name = typeDecl.Identifier.Text;
        var arity = typeDecl.TypeParameterList?.Parameters.Count ?? 0;

        return arity > 0 ? $"{name}`{arity}" : name;
    }

    private static string GetContainingNamespace(SyntaxNode node)
    {
        var namespaceParts = new List<string>();
        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax nsDecl)
                namespaceParts.Insert(0, nsDecl.Name.ToString());

            parent = parent.Parent;
        }

        return string.Join(".", namespaceParts);
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
}