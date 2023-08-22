using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class EndpointsDiscoveryGenerator : IIncrementalGenerator
{
     public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarationSyntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
                (sn, _) => sn is TypeDeclarationSyntax,
                (c, _) => Transform(c))
            .Where(p => p is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(typeDeclarationSyntaxProvider.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Right!, spc));
    }

    private void Execute(ImmutableArray<ITypeSymbol> typeSymbols, SourceProductionContext spc)
    {
        if (!typeSymbols.Any())
            return;
        var fileContent = GetContent(typeSymbols);
        spc.AddSource(
            "DiscoveredTypes.g.cs",
            SourceText.From(fileContent,
                Encoding.UTF8));
    }

    private ITypeSymbol? Transform(GeneratorSyntaxContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node);
        if (symbol is not ITypeSymbol typeSymbol) return null;
        var isValid = typeSymbol.AllInterfaces.Select(i => new TypeDescription(i)).Intersect(new[]
        {
            new TypeDescription("FastEndpoints.IEndpoint"),
            new TypeDescription("FastEndpoints.IEventHandler"),
            new TypeDescription("FastEndpoints.ICommandHandler"),
            new TypeDescription("FastEndpoints.ISummary"),
            new TypeDescription("FluentValidation.IValidator")
        }).Any() && typeSymbol is { IsAbstract: false, DeclaredAccessibility: Accessibility.Public };
        return isValid ? typeSymbol : null;
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
            sb.Append("            typeof(")
                .Append(discoveredType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(@"),
");
        }

        sb.Append(@"        };
    }
}");
        return sb.ToString();
    }
}
