using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    // ReSharper disable once InconsistentNaming
    static readonly StringBuilder b = new();
    static string? _assemblyName;
    const string DontRegisterAttribute = "DontRegisterAttribute";

    static readonly string[] _whiteList =
    [
        "FastEndpoints.IEndpoint",
        "FastEndpoints.IEventHandler",
        "FastEndpoints.ICommandHandler",
        "FastEndpoints.ISummary",
        "FluentValidation.IValidator"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var syntaxProvider = ctx.SyntaxProvider
                                .CreateSyntaxProvider(Qualify, Transform)
                                .Where(static t => t is not null)
                                .Collect();

        ctx.RegisterSourceOutput(syntaxProvider, Generate!);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            return
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type ||
                type.IsAbstract ||
                type.GetAttributes().Any(a => a.AttributeClass!.Name == DontRegisterAttribute || type.AllInterfaces.Length == 0)
                    ? null
                    : type.AllInterfaces.Any(i => _whiteList.Contains(i.ToDisplayString()))
                        ? type.ToDisplayString()
                        : null;
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, ImmutableArray<string> typeNames)
    {
        if (typeNames.Length == 0)
            return;

        var fileContent = RenderClass(typeNames);
        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    static string RenderClass(ImmutableArray<string> discoveredTypes)
    {
        b.Clear().w(
            $$"""
              #pragma warning disable CS0618

              namespace {{_assemblyName}};

              using System;

              public static class DiscoveredTypes
              {
                  public static readonly List<Type> All =
                  [
              """);

        foreach (var t in discoveredTypes.Distinct().OrderBy(t => t))
        {
            b.w(
                $"""
                 
                         typeof({t}),
                 """);
        }
        b.w(
            """
            
                ];
            }
            """);

        return b.ToString();
    }
}