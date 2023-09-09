using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var typeDeclarationSyntaxProvider = ctx.SyntaxProvider.CreateSyntaxProvider(
            static (sn, _) => sn is TypeDeclarationSyntax,
            static (c, _) => Transform(c)
        ).Where(static t => t is not null);

        var compilationAndClasses = ctx.CompilationProvider.Combine(typeDeclarationSyntaxProvider.Collect());

        ctx.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static ITypeSymbol? Transform(GeneratorSyntaxContext ctx)
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
        }).Any() && typeSymbol is { IsAbstract: false };
        return isValid ? typeSymbol : null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ITypeSymbol> typeSymbols, SourceProductionContext spc)
    {
        if (!typeSymbols.Any()) return;
        var fileContent = GetContent(compilation, typeSymbols);
        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    private static readonly StringBuilder b = new();
    private static int count;

    private static string GetContent(Compilation compilation, IEnumerable<ITypeSymbol> discoveredTypes)
    {
        count++;

        var assembly = compilation.AssemblyName;
        b.Clear().w(
"namespace ").w(assembly).w(@"
{

//count: ").w(count.ToString()).w(@"

    public static class DiscoveredTypes
    {
        public static readonly global::System.Type[] All = new global::System.Type[]
        {");
        foreach (var t in discoveredTypes)
        {
            b.w(@"
            typeof(").w(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).w("),");
        }
        b.w(@"
        };
    }
}");
        return b.ToString();
    }
}