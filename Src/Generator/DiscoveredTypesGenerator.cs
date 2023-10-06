using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    static string? _assemblyName;
    static readonly StringBuilder b = new();
    const string _dontRegisterAttribute = "DontRegisterAttribute";

    static readonly string[] _whiteList = new[]
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
            .CreateSyntaxProvider(Match, Transform)
            .Where(static t => t is not null)
            .Collect();

        ctx.RegisterSourceOutput(syntaxProvider, Generate!);

        static bool Match(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax cds && cds.TypeParameterList is null;

        static string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            return
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type ||
                type.IsAbstract ||
                type.GetAttributes().Any(a => a.AttributeClass!.Name == _dontRegisterAttribute || type.AllInterfaces.Length == 0)
                    ? null
                    : type.AllInterfaces.Any(i => _whiteList.Contains(i.ToDisplayString()))
                        ? type.ToDisplayString()
                        : null;
        }
    }

    static void Generate(SourceProductionContext spc, ImmutableArray<string> typeNames)
    {
        if (!typeNames.Any()) return;
        var fileContent = RenderClass(typeNames.OrderBy(t => t));
        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    static string RenderClass(IEnumerable<string> discoveredTypes)
    {
        b.Clear().w(
"namespace ").w(_assemblyName).w(@";

using System;

public static class DiscoveredTypes
{
    public static readonly Type[] All = new Type[]
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