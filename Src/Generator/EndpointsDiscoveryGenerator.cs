using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace FastEndpoints.Generator;

[Generator]
public class EndpointsDiscoveryGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext ctx)
    {
        var excludes = new[]
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

        var mainTypes = GetAssemblySymbolTypes(ctx.Compilation.SourceModule.ContainingAssembly);
        var referencedTypes = ctx.Compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(GetAssemblySymbolTypes);
        var filteredTypes = mainTypes.Concat(referencedTypes).Where(t =>
                !t.IsAbstract &&
                !excludes.Any(n => GetRootNamespaceSymbolFor(t).Name.StartsWith(n, StringComparison.OrdinalIgnoreCase)) &&
                t.DeclaredAccessibility == Accessibility.Public &&
                t.AllInterfaces.Select(i => new TypeDescription(i)).Intersect(new[] {
                    new TypeDescription("FastEndpoints.IEndpoint"),
                    new TypeDescription("FluentValidation.IValidator"),
                    new TypeDescription("FastEndpoints.IEventHandler"),
                    new TypeDescription("FastEndpoints.ISummary")
                }).Any());

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
            sb.Append("            typeof(").Append(discoveredType).Append(@"),
");
        }

        sb.Append(@"        };
    }
}");

        ctx.AddSource(
            "DiscoveredTypes.g.cs",
            SourceText.From(sb.ToString(),
            Encoding.UTF8));
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

    private static IEnumerable<ITypeSymbol> GetAssemblySymbolTypes(IAssemblySymbol a) => GetAllTypes(a.GlobalNamespace);
}