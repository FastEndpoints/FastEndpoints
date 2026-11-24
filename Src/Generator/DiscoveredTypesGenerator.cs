using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class DiscoveredTypesGenerator : IIncrementalGenerator
{
    static readonly string[] _whiteList =
    [
        "FastEndpoints.IEndpoint",
        "FastEndpoints.IEventHandler",
        "FastEndpoints.ICommandHandler",
        "FastEndpoints.IStreamCommandHandler<",
        "FastEndpoints.ISummary",
        "FastEndpoints.IJobStorageProvider<",
        "FastEndpoints.IPreProcessor<",
        "FastEndpoints.IPostProcessor<",
        "FastEndpoints.ICommandMiddleware<",
        "FastEndpoints.IStreamCommandMiddleware<",
        "FastEndpoints.IMapper",
        "FluentValidation.IValidator"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var syntaxProvider = initCtx.SyntaxProvider
                                    .CreateSyntaxProvider(Qualify, Transform)
                                    .Where(static t => t is not null)
                                    .Collect();

        initCtx.RegisterSourceOutput(syntaxProvider.Combine(initCtx.RootNamespaceProvider()), Generate);

        //executed per each keystroke.
        //a type implements interfaces only if at least one of its declarations has a base list.
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax or RecordDeclarationSyntax &&
               node is TypeDeclarationSyntax { TypeParameterList: null, BaseList: not null } &&
               !node.IsNestedInGenericType();

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static string? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
            => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is ITypeSymbol { IsAbstract: false } type &&
               !type.HasDontRegisterAttribute() &&
               type.AllInterfaces.Any(static i => IsWhiteListed(i.ToDisplayString()))
                   ? type.ToFullName()
                   : null;

        static bool IsWhiteListed(string interfaceName)
            => _whiteList.Any(w => interfaceName.StartsWith(w, StringComparison.Ordinal));
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
