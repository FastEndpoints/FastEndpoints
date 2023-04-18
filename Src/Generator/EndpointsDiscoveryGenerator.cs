using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class EndpointsDiscoveryGenerator : IIncrementalGenerator
{
    //also update FastEndpoints.EndpointData class if updating these
    private static readonly string[] _excludes = new string[]
    {
            "Microsoft",
            "System",
            "FastEndpoints",
            "testhost",
            "netstandard",
            "Newtonsoft",
            "mscorlib",
            "NuGet",
            "NSwag",
            "FluentValidation",
            "YamlDotNet",
            "Accessibility",
            "NJsonSchema",
            "Namotion"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarationSyntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
            (sn, _) => sn is TypeDeclarationSyntax,
            (c, _) => (TypeDeclarationSyntax)c.Node);

        var compilationAndClasses = context.CompilationProvider.Combine(typeDeclarationSyntaxProvider.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private void Execute(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> typeDeclarationSyntaxProvider, SourceProductionContext spc)
    {
        var filteredTypes = GetFilteredTypes(compilation, typeDeclarationSyntaxProvider);
        if (!filteredTypes.Any())
            return;
        var fileContent = GetContent(filteredTypes!);
        spc.AddSource(
          "DiscoveredTypes.g.cs",
          SourceText.From(fileContent,
          Encoding.UTF8));
    }

    private IEnumerable<ITypeSymbol> GetFilteredTypes(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> typeDeclarationSyntaxProvider)
    {
        var mainTypes = GetAssemblySymbolTypes(compilation.SourceModule.ContainingAssembly);
        var referencedTypes = compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(GetAssemblySymbolTypes);
        return mainTypes.Concat(referencedTypes).Where(t =>
            !t.IsAbstract &&
            !_excludes.Any(n => GetRootNamespaceSymbolFor(t).Name.StartsWith(n, StringComparison.OrdinalIgnoreCase)) &&
            t.DeclaredAccessibility == Accessibility.Public &&
            t.AllInterfaces.Select(i => new TypeDescription(i)).Intersect(new[]
            {
                new TypeDescription("FastEndpoints.IEndpoint"),
                new TypeDescription("FastEndpoints.IEventHandler"),
                new TypeDescription("FastEndpoints.ICommandHandler"),
                new TypeDescription("FastEndpoints.ISummary"),
                new TypeDescription("FluentValidation.IValidator")
            }).Any());
    }

    private static string GetContent(IEnumerable<ITypeSymbol> filteredTypes)
    {
        var sb = new StringBuilder(@"
using System;
namespace FastEndpoints
{
    public static class DiscoveredTypes
    {
        public static readonly System.Type[] All = new System.Type[]
        {
");

        foreach (var discoveredType in filteredTypes)
        {
            sb.Append("            typeof(").Append(discoveredType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(@"),
");
        }

        sb.Append(@"        };
    }
}");
        return sb.ToString();
    }

    private static INamespaceSymbol GetRootNamespaceSymbolFor(ITypeSymbol symbol)
    {
        var currentNamespace = symbol.ContainingNamespace;

        while (true)
        {
            var parentNamespace = currentNamespace.ContainingNamespace;

            if (parentNamespace?.IsGlobalNamespace != false)
                return currentNamespace;

            currentNamespace = parentNamespace;
        }
    }

    private static IEnumerable<ITypeSymbol> GetAllTypes(INamespaceSymbol root)
    {
        foreach (var namespaceOrTypeSymbol in root.GetMembers())
        {
            switch (namespaceOrTypeSymbol)
            {
                case INamespaceSymbol @namespace:
                    {
                        foreach (var nested in GetAllTypes(@namespace))
                            yield return nested;
                        break;
                    }
                case ITypeSymbol type:
                    yield return type;
                    break;
            }
        }
    }

    private static IEnumerable<ITypeSymbol> GetAssemblySymbolTypes(IAssemblySymbol a)
        => GetAllTypes(a.GlobalNamespace);
}
