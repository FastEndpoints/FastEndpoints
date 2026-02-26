using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    const string DontRegisterAttribute = "DontRegisterAttribute";

    static readonly string[] _whiteList =
    [
        "FastEndpoints.IEndpoint",
        "FastEndpoints.IEventHandler",
        "FastEndpoints.ICommandHandler",
        "FastEndpoints.ISummary",
        "FastEndpoints.IJobStorageProvider<",
        "FastEndpoints.IPreProcessor<",
        "FastEndpoints.IPostProcessor<",
        "FastEndpoints.ICommandMiddleware<",
        "FastEndpoints.IMapper",
        "FluentValidation.IValidator"
    ];

    // ReSharper disable once InconsistentNaming
    readonly StringBuilder b = new();
    string? _rootNamespace;

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var syntaxProvider = initCtx.SyntaxProvider
                                    .CreateSyntaxProvider(Qualify, Transform)
                                    .Where(static t => t is not null)
                                    .Collect();

        initCtx.RegisterSourceOutput(syntaxProvider, Generate!);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _rootNamespace = ctx.SemanticModel.Compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly";

            return
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type ||
                type.IsAbstract ||
                type.GetAttributes().Any(a => a.AttributeClass!.Name == DontRegisterAttribute || type.AllInterfaces.Length == 0)
                    ? null
                    : type.AllInterfaces.Any(i => _whiteList.Any(w => i.ToDisplayString().StartsWith(w)))
                        ? type.ToDisplayString()
                        : null;
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    void Generate(SourceProductionContext spc, ImmutableArray<string> typeNames)
    {
        if (typeNames.Length == 0)
            return;

        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(RenderClass(typeNames), Encoding.UTF8));
    }

    string RenderClass(ImmutableArray<string> discoveredTypes)
    {
        b.Clear().w(
            $$"""
              #pragma warning disable CS0618

              namespace {{_rootNamespace}};

              using System;
              using System.Diagnostics.CodeAnalysis;

              public static class DiscoveredTypes
              {
                  public static readonly List<Type> All =
                  [
              """);

        foreach (var t in discoveredTypes.Distinct().OrderBy(t => t))
        {
            b.w(
                $"""

                         Preserve<{t}>(),
                 """);
        }
        b.w(
            """

                ];
                
                // this method instructs the native aot linker to not strip away metadata on a given type
                static Type Preserve<
                    [DynamicallyAccessedMembers(
                        DynamicallyAccessedMemberTypes.PublicConstructors | 
                        DynamicallyAccessedMemberTypes.PublicMethods | 
                        DynamicallyAccessedMemberTypes.Interfaces)]T>() => typeof(T);
            }
            """);

        return b.ToString();
    }
}