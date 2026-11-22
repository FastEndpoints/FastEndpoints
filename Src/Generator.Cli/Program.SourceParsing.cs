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

    private static (SourceIndex Index, List<EndpointDeclaration> Endpoints) BuildAnalysisInputs(ConcurrentBag<FileParseResult> parseResults)
    {
        var globalUsings = new HashSet<string>(StringComparer.Ordinal);
        var globalTypeAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignoredGlobalAliases = new HashSet<string>(StringComparer.Ordinal);
        var typeDeclarations = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);
        var usingsByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var typeAliasesByFile = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var endpoints = new List<EndpointDeclaration>();

        // Sort by file path to ensure deterministic merging of partial classes
        var sortedResults = parseResults.OrderBy(r => r.FilePath, StringComparer.Ordinal).ToList();

        foreach (var result in sortedResults)
        {
            endpoints.AddRange(result.Endpoints);
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
                TypeInfo.AddOrMerge(typeDeclarations, kvp.Key, kvp.Value);
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

        return (new(typeDeclarations, allUsingsByFile, allTypeAliasesByFile), endpoints);
    }

    private static FileParseResult ParseFile(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
        var root = tree.GetRoot();

        var walker = new SourceFileWalker(filePath);
        walker.Visit(root);

        return new(filePath, walker.FileUsings, walker.GlobalUsings, walker.FileTypeAliases, walker.GlobalTypeAliases, walker.Types, walker.Endpoints);
    }

    private sealed class SourceFileWalker(string filePath) : CSharpSyntaxWalker
    {
        public List<string> FileUsings { get; } = [];
        public List<string> GlobalUsings { get; } = [];
        public Dictionary<string, string> FileTypeAliases { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> GlobalTypeAliases { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, TypeInfo> Types { get; } = new(StringComparer.Ordinal);
        public List<EndpointDeclaration> Endpoints { get; } = [];

        public override void VisitUsingDirective(UsingDirectiveSyntax usingDirective)
        {
            if (usingDirective.Name == null || usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            {
                base.VisitUsingDirective(usingDirective);
                return;
            }

            var usingName = usingDirective.Name.ToString();

            if (usingDirective.Alias != null)
            {
                var aliasName = usingDirective.Alias.Name.ToString();
                if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                    GlobalTypeAliases[aliasName] = usingName;
                else
                    FileTypeAliases[aliasName] = usingName;
                
                base.VisitUsingDirective(usingDirective);
                return;
            }

            if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                GlobalUsings.Add(usingName);
            else
                FileUsings.Add(usingName);

            base.VisitUsingDirective(usingDirective);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node);
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node);
            base.VisitStructDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            ProcessTypeDeclaration(node);
            base.VisitRecordDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            ProcessEnumDeclaration(node);
            base.VisitEnumDeclaration(node);
        }

        private void ProcessTypeDeclaration(TypeDeclarationSyntax typeDecl)
        {
            if (typeDecl.Modifiers.Any(m => m.Text == "file"))
                return;

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
            
            TypeInfo.AddOrMerge(Types, fullName, typeInfo);

            if (typeDecl is ClassDeclarationSyntax && FindEndpointBaseType(baseTypes) is { } endpointBase)
                Endpoints.Add(new(filePath, fullName, ns, endpointBase));
        }

        private void ProcessEnumDeclaration(EnumDeclarationSyntax enumDecl)
        {
            if (enumDecl.Modifiers.Any(m => m.Text == "file"))
                return;

            var fullName = GetFullTypeName(enumDecl);
            var baseTypes = enumDecl.BaseList?.Types.Select(t => new TypeRef(t.ToString(), filePath)).ToList() ?? [];
            var ns = GetContainingNamespace(enumDecl);
            var typeInfo = new TypeInfo(fullName, ns, enumDecl.Identifier.Text, filePath, [], baseTypes, []);

            TypeInfo.AddOrMerge(Types, fullName, typeInfo);
        }
    }

    private static string GetFullTypeName(BaseTypeDeclarationSyntax typeDecl)
    {
        var nameParts = new List<string>
        {
            typeDecl is TypeDeclarationSyntax td ? GetTypeNameWithArity(td) : typeDecl.Identifier.Text
        };
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
}