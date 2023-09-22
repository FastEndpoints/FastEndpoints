using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    private static readonly StringBuilder b = new();
    private static string? _assemblyName;
    private static readonly string[] _whiteList = new[]
    {
        "FastEndpoints.IEndpoint",
        "FastEndpoints.IEventHandler",
        "FastEndpoints.ICommandHandler",
        "FastEndpoints.ISummary",
        "FluentValidation.IValidator"
    };

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var syntaxProvider = ctx.SyntaxProvider
            .CreateSyntaxProvider(static (sn, _) => sn is ClassDeclarationSyntax, Transform)
            .Where(static t => t is not null)
            .Collect();

        ctx.RegisterSourceOutput(syntaxProvider, Generate!);

        static string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is ITypeSymbol type &&
               !type.IsAbstract &&
               !type.GetAttributes().Any(a => a.AttributeClass!.Name == "DontRegisterAttribute") &&
                type.AllInterfaces.Length > 0 &&
                type.AllInterfaces.Any(i => _whiteList.Contains($"{i.ContainingNamespace.Name}.{i.Name}")))
            {
                return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            return null;
        }
    }

    private static void Generate(SourceProductionContext spc, ImmutableArray<string> typeNames)
    {
        if (!typeNames.Any()) return;
        var fileContent = RenderClass(typeNames.OrderBy(t => t));
        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    private static string RenderClass(IEnumerable<string> discoveredTypes)
    {
        b.Clear().w(
"namespace ").w(_assemblyName).w(@";

public static class DiscoveredTypes
{
    public static readonly global::System.Type[] All = new global::System.Type[]
    {");
        foreach (var t in discoveredTypes)
        {
            b.w(@"
        typeof(").w(t).w("),");
        }
        b.w(@"
    };
}");
        return b.ToString();
    }
}