using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class GenericProcessorTypesGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string IPreProcessor = "FastEndpoints.IPreProcessor<";
    const string IPostProcessor = "FastEndpoints.IPostProcessor<";
    const string DontRegisterAttribute = "DontRegisterAttribute";

    readonly StringBuilder b = new();
    string? _rootNamespace;

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var provider = initCtx.SyntaxProvider
                              .CreateSyntaxProvider(Qualify, Transform)
                              .Where(static m => m.IsValid)
                              .WithComparer(TypeDataComparer.Instance)
                              .Collect();

        initCtx.RegisterSourceOutput(provider, Generate);

        //executed per each keystroke - filter to classes only for efficiency
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax;
    }

    //executed per each keystroke but only for syntax nodes filtered by the Qualify method
    TypeData Transform(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        //should be re-assigned on every call. do not cache!
        _rootNamespace = ctx.SemanticModel.Compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly";

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol type ||
            type.IsAbstract ||
            type.GetAttributes().Any(a => a.AttributeClass?.Name == DontRegisterAttribute))
            return TypeData.Invalid;

        var fullName = type.ToDisplayString();
        var isOpenGeneric = type.IsGenericType && type.TypeArguments.All(t => t.Kind == SymbolKind.TypeParameter);

        foreach (var ifc in type.AllInterfaces)
        {
            var ifcName = ifc.ToDisplayString();

            // IPreProcessor<TRequest>
            if (isOpenGeneric && ifcName.StartsWith(IPreProcessor) && type.TypeArguments.Length == 1)
                return new(fullName, TypeKind.OpenGenericPreProcessor, type.TypeArguments.Length);

            // IPostProcessor<TRequest, TResponse>
            if (isOpenGeneric && ifcName.StartsWith(IPostProcessor) && type.TypeArguments.Length == 2)
                return new(fullName, TypeKind.OpenGenericPostProcessor, type.TypeArguments.Length);

            // Endpoint<TRequest,TResponse>
            if (ifcName == IEndpoint && type.BaseType is { } baseType && baseType.IsGenericType)
            {
                var reqType = baseType.TypeArguments.Length > 0 ? baseType.TypeArguments[0].ToDisplayString() : null;
                var resType = baseType.TypeArguments.Length > 1 ? baseType.TypeArguments[1].ToDisplayString() : null;

                return new(fullName, TypeKind.Endpoint, 0, reqType, resType);
            }
        }

        return TypeData.Invalid;
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    void Generate(SourceProductionContext ctx, ImmutableArray<TypeData> types)
    {
        var openGenericPreProcessors = types.Where(t => t.Kind == TypeKind.OpenGenericPreProcessor).ToList();
        var openGenericPostProcessors = types.Where(t => t.Kind == TypeKind.OpenGenericPostProcessor).ToList();
        var endpoints = types.Where(t => t.Kind == TypeKind.Endpoint).ToList();

        if (openGenericPreProcessors.Count == 0 && openGenericPostProcessors.Count == 0)
        {
            b.Clear().w(
                """

                // No open generic pre/post processors found

                """);
            ctx.AddSource("GenericProcessorTypes.g.cs", SourceText.From(b.ToString(), Encoding.UTF8));

            return;
        }

        var requestTypes = endpoints.Select(e => e.RequestType).Where(r => r != null).Distinct().ToList();
        var responseTypes = endpoints.Select(e => e.ResponseType).Where(r => r != null).Distinct().ToList();

        b.Clear().w(
            $$"""
              #pragma warning disable CS0618

              namespace {{_rootNamespace}};

              using System;
              using System.Diagnostics.CodeAnalysis;

              public static class GenericProcessorTypes
              {
                  public static readonly List<Type> All =
                  [
              """);

        foreach (var processor in openGenericPreProcessors)
        {
            var baseName = GetTypeNameWithoutGeneric(processor.FullName);

            if (baseName == null)
                continue;

            foreach (var reqType in requestTypes)
            {
                b.w(
                    $"""

                             Preserve<{baseName}<{reqType}>>(),
                     """);
            }
        }

        foreach (var processor in openGenericPostProcessors)
        {
            var baseName = GetTypeNameWithoutGeneric(processor.FullName);

            if (baseName == null)
                continue;

            foreach (var reqType in requestTypes)
            {
                foreach (var resType in responseTypes)
                {
                    b.w(
                        $"""

                                 Preserve<{baseName}<{reqType}, {resType}>>(),
                         """);
                }
            }
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

        ctx.AddSource("GenericProcessorTypes.g.cs", SourceText.From(b.ToString(), Encoding.UTF8));
    }

    static string? GetTypeNameWithoutGeneric(string fullName)
    {
        var genericIndex = fullName.IndexOf('<');

        return genericIndex > 0 ? fullName.Substring(0, genericIndex) : null;
    }

    readonly struct TypeData(string fullName, TypeKind kind, int typeParamCount, string? requestType = null, string? responseType = null)
    {
        public string FullName { get; } = fullName;
        public TypeKind Kind { get; } = kind;
        public int TypeParamCount { get; } = typeParamCount;
        public string? RequestType { get; } = requestType;
        public string? ResponseType { get; } = responseType;
        public bool IsValid => Kind != TypeKind.Invalid;

        public static TypeData Invalid => new();
    }

    enum TypeKind
    {
        Invalid,
        OpenGenericPreProcessor,
        OpenGenericPostProcessor,
        Endpoint
    }

    sealed class TypeDataComparer : IEqualityComparer<TypeData>
    {
        internal static TypeDataComparer Instance { get; } = new();

        TypeDataComparer() { }

        public bool Equals(TypeData x, TypeData y)
            => x.FullName.Equals(y.FullName) && x.Kind == y.Kind && x.TypeParamCount == y.TypeParamCount;

        public int GetHashCode(TypeData obj)
            => obj.FullName.GetHashCode() ^ obj.Kind.GetHashCode() ^ obj.TypeParamCount.GetHashCode();
    }
}