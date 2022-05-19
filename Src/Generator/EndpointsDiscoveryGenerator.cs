using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Text;

namespace FastEndpoints.Generator;

[Generator]
public class EndpointsDiscoveryGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // list of excluded namespaces
        var excludes = new[] {
            "Microsoft",
            "System",
            "FastEndpoints",
            "testhost",
            "netstandard",
            "Newtonsoft",
            "mscorlib",
            "NuGet",
            "NSwag",
            "FluentValidation"
        };

        var mainTypes = GetAssemblySymbolTypes(context.Compilation.SourceModule.ContainingAssembly);

        var referencedTypes =
            context.Compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(GetAssemblySymbolTypes);

        var types = mainTypes.Concat(referencedTypes);

        var filteredTypes = types
            .Where(t =>
                !excludes.Any(n =>
                    GetRootNamespaceSymbolFor(t).Name.StartsWith(n, StringComparison.OrdinalIgnoreCase)) &&
                t.AllInterfaces.Select(i => i.Name).Intersect(new[] {
                    "IEndpoint",
                    "IValidator",
                    "IEventHandler",
                    "ISummary"
                }).Any()
            ).ToList();

        var sourceBuilder = new StringBuilder(@"using System;
namespace FastEndpoints
{
    public static class DiscoveredTypes
    {
        public static readonly System.Type[] AllTypes = new System.Type[]
        {");

        foreach (var discoveredType in filteredTypes)
            sourceBuilder.Append(@$"
            typeof({discoveredType}),
");

        sourceBuilder.Append(@"
        };
    }
}
");

        context.AddSource("FastEndpointsDiscoveredTypesGenerated.g.cs",
            SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private static INamespaceSymbol GetRootNamespaceSymbolFor(ITypeSymbol symbol)
    {
        var currentNamespace = symbol.ContainingNamespace;

        while (true)
        {
            var parentNamespace = currentNamespace.ContainingNamespace;

            if (parentNamespace is null || parentNamespace.IsGlobalNamespace)
                return currentNamespace;

            currentNamespace = parentNamespace;
        }
    }

    private static IEnumerable<ITypeSymbol> GetAllTypes(INamespaceSymbol root)
    {
        foreach (var namespaceOrTypeSymbol in root.GetMembers())
            switch (namespaceOrTypeSymbol)
            {
                case INamespaceSymbol @namespace:
                {
                    foreach (var nested in GetAllTypes(@namespace)) yield return nested;
                    break;
                }
                case ITypeSymbol type:
                    yield return type;
                    break;
            }
    }

    private static IEnumerable<ITypeSymbol> GetAssemblySymbolTypes(IAssemblySymbol a)
    {
        try
        {
            var main = a.Identity.Name.Split('.').Aggregate(a.GlobalNamespace,
                (s, c) => s.GetNamespaceMembers().Single(m => m.Name.Equals(c)));

            return GetAllTypes(main);
        }
        catch
        {
            return Enumerable.Empty<ITypeSymbol>();
        }
    }
}