using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    //metadata names (see Extensions.MetadataFullName) of the interfaces whose implementations are discovered
    static readonly HashSet<string> _whiteList =
    [
        Extensions.IEndpoint,
        "FastEndpoints.IEventHandler",
        "FastEndpoints.IEventHandler`1",
        "FastEndpoints.ICommandHandler",
        "FastEndpoints.ICommandHandler`1",
        "FastEndpoints.ICommandHandler`2",
        "FastEndpoints.IStreamCommandHandler`2",
        "FastEndpoints.ISummary",
        "FastEndpoints.IJobStorageProvider`1",
        Extensions.IPreProcessor,
        Extensions.IPostProcessor,
        "FastEndpoints.ICommandMiddleware`2",
        "FastEndpoints.IStreamCommandMiddleware`2",
        "FastEndpoints.IMapper",
        "FluentValidation.IValidator",
        "FluentValidation.IValidator`1"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var syntaxProvider = initCtx.SyntaxProvider
                                    .CreateSyntaxProvider(Qualify, Transform)
                                    .Where(static t => t is not null)
                                    .Collect();

        initCtx.RegisterSourceOutput(syntaxProvider.Combine(initCtx.RootNamespaceProvider()), Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is TypeDeclarationSyntax { TypeParameterList: null } && node.IsCandidateTypeDeclaration();

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
            => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is ITypeSymbol type &&
               type.IsRegistrable() &&
               type.AllInterfaces.Any(static i => _whiteList.Contains(i.MetadataFullName()))
                   ? type.ToFullName()
                   : null;
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, (ImmutableArray<string?> TypeNames, string RootNamespace) input)
    {
        if (input.TypeNames.Length == 0)
            return;

        spc.AddSource("DiscoveredTypes.g.cs", SourceText.From(RenderClass(input.TypeNames, input.RootNamespace), Encoding.UTF8));
    }

    static string RenderClass(ImmutableArray<string?> discoveredTypes, string rootNamespace)
        => Extensions.RenderPreservedTypeList(
            rootNamespace,
            "DiscoveredTypes",
            discoveredTypes.Distinct().OrderBy(static t => t, StringComparer.Ordinal)!);
}
