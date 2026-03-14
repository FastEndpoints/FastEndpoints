using Microsoft.CodeAnalysis;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace FastEndpoints.Generator.Cli;

record SourceFileSet(List<string> Files, string Hash);

record NuGetPackageAssemblySet(List<NuGetPackageAssemblyInfo> PackageAssemblies, List<string> ReferenceAssemblies, string Hash);

record NuGetPackageAssemblyInfo(string LibraryKey, string AssemblyPath);

readonly record struct TargetFrameworkMoniker(string Identifier, Version Version)
{
    public string BaseMoniker => $"{Identifier}{Version.Major}.{Version.Minor}";
}

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