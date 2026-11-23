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
    const string IPreProcessor = "FastEndpoints.IPreProcessor<";
    const string IPostProcessor = "FastEndpoints.IPostProcessor<";

    static readonly SymbolDisplayFormat _openGenericNameFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None);

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var provider = initCtx.SyntaxProvider
                              .CreateSyntaxProvider(Qualify, Transform)
                              .Where(static m => m is not null)
                              .Collect();

        initCtx.RegisterSourceOutput(provider.Combine(initCtx.RootNamespaceProvider()), Generate);

        //executed per each keystroke - filter to classes that can implement an interface or derive from an endpoint base
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { BaseList: not null } && !node.IsNestedInGenericType();
    }

    //executed per each keystroke but only for syntax nodes filtered by the Qualify method
    static TypeData? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol type ||
            type.IsAbstract ||
            type.HasDontRegisterAttribute())
            return null;

        if (type.IsGenericType)
        {
            foreach (var ifc in type.AllInterfaces)
            {
                var ifcName = ifc.ToDisplayString();

                // IPreProcessor<TRequest>
                if (type.TypeParameters.Length == 1 && ifcName.StartsWith(IPreProcessor, StringComparison.Ordinal))
                    return Processor(TypeKind.OpenGenericPreProcessor);

                // IPostProcessor<TRequest, TResponse>
                if (type.TypeParameters.Length == 2 && ifcName.StartsWith(IPostProcessor, StringComparison.Ordinal))
                    return Processor(TypeKind.OpenGenericPostProcessor);
            }

            return null; //open generic endpoints are not discovered, so their type parameters must not be emitted
        }

        // Endpoint<TRequest, TResponse>
        return type.GetEndpointBase() is { } epBase
                   ? new(TypeKind.Endpoint, type.ToFullName(), default, Dto.From(epBase.TypeArguments[0]), Dto.From(epBase.TypeArguments[1]))
                   : null;

        TypeData Processor(TypeKind kind)
            => new(kind, type.ToDisplayString(_openGenericNameFormat), new(type.TypeParameters.Select(Constraints.From).ToArray()));
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext ctx, (ImmutableArray<TypeData?> Types, string RootNamespace) input)
    {
        var types = input.Types.Select(static t => t!).ToArray();
        var preProcessors = ProcessorsOf(TypeKind.OpenGenericPreProcessor);
        var postProcessors = ProcessorsOf(TypeKind.OpenGenericPostProcessor);

        if (preProcessors.Length == 0 && postProcessors.Length == 0)
        {
            ctx.AddSource(
                "GenericProcessorTypes.g.cs",
                SourceText.From(
                    """

                    // No open generic pre/post processors found

                    """,
                    Encoding.UTF8));

            return;
        }

        var endpoints = types.Where(static t => t.Kind == TypeKind.Endpoint).ToArray();

        //processors are closed at runtime with the request/response types of the endpoint they're attached to
        var requestTypes = endpoints.Select(static e => e.Request!)
                                    .Distinct()
                                    .OrderBy(static t => t.Name, StringComparer.Ordinal)
                                    .ToArray();
        var dtoPairs = endpoints.Select(static e => (Request: e.Request!, Response: e.Response!))
                                .Distinct()
                                .OrderBy(static p => p.Request.Name, StringComparer.Ordinal)
                                .ThenBy(static p => p.Response.Name, StringComparer.Ordinal)
                                .ToArray();

        var typeExpressions = new List<string>();

        foreach (var processor in preProcessors)
        {
            //a processor attached to an endpoint whose dto violates its constraints would fail at runtime anyway
            foreach (var reqType in requestTypes.Where(r => r.Satisfies(processor.Constraints[0])))
                typeExpressions.Add($"{processor.Name}<{reqType.Name}>");
        }

        foreach (var processor in postProcessors)
        {
            foreach (var (req, res) in dtoPairs.Where(p => p.Request.Satisfies(processor.Constraints[0]) && p.Response.Satisfies(processor.Constraints[1])))
                typeExpressions.Add($"{processor.Name}<{req.Name}, {res.Name}>");
        }

        ctx.AddSource(
            "GenericProcessorTypes.g.cs",
            SourceText.From(Extensions.RenderPreservedTypeList(input.RootNamespace, "GenericProcessorTypes", typeExpressions), Encoding.UTF8));

        TypeData[] ProcessorsOf(TypeKind kind)
            => types.Where(t => t.Kind == kind)
                    .Distinct()
                    .OrderBy(static t => t.Name, StringComparer.Ordinal)
                    .ToArray();
    }

    sealed record TypeData(
        TypeKind Kind,
        string Name,
        EquatableArray<Constraints> Constraints = default,
        Dto? Request = null,
        Dto? Response = null);

    //the constraints of a processor's type parameter. constraint types referring to type parameters can't be checked by name and are ignored.
    sealed record Constraints(bool Class, bool Struct, bool Unmanaged, bool New, EquatableArray<string> Types)
    {
        internal static Constraints From(ITypeParameterSymbol p)
            => new(
                p.HasReferenceTypeConstraint,
                p.HasValueTypeConstraint,
                p.HasUnmanagedTypeConstraint,
                p.HasConstructorConstraint,
                new(p.ConstraintTypes.Where(static t => !ContainsTypeParameter(t)).Select(static t => t.ToFullName()).ToArray()));

        static bool ContainsTypeParameter(ITypeSymbol t)
            => t switch
            {
                ITypeParameterSymbol => true,
                IArrayTypeSymbol a => ContainsTypeParameter(a.ElementType),
                INamedTypeSymbol n => n.TypeArguments.Any(ContainsTypeParameter),
                _ => false
            };
    }

    sealed record Dto(string Name, bool IsReferenceType, bool IsValueType, bool IsUnmanagedType, bool HasDefaultCtor, EquatableArray<string> AssignableTo)
    {
        internal static Dto From(ITypeSymbol t)
        {
            var isValueType = t.IsValueType && t.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

            return new(
                t.ToFullName(),
                t.IsReferenceType,
                isValueType,
                isValueType && t.IsUnmanagedType,
                CanConstructWithNew(t),
                new(BaseTypesOf(t).Concat(t.AllInterfaces).Select(static b => b.ToFullName()).ToArray()));
        }

        internal bool Satisfies(Constraints c)
            => (!c.Class || IsReferenceType) &&
               (!c.Struct || IsValueType) &&
               (!c.Unmanaged || IsUnmanagedType) &&
               (!c.New || HasDefaultCtor) &&
               c.Types.All(AssignableTo.Contains);

        static bool CanConstructWithNew(ITypeSymbol t)
        {
            if (t.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
                return true;

            if (t is not INamedTypeSymbol { IsAbstract: false } n)
                return false;

            var ctor = n.InstanceConstructors.FirstOrDefault(static c => c is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 0 });

            if (ctor is null)
                return false;

            return !HasRequiredMembers(t) ||
                   ctor.GetAttributes().Any(static a => a.AttributeClass?.ToFullName() == "global::System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
        }

        static bool HasRequiredMembers(ITypeSymbol t)
            => BaseTypesOf(t).Any(static b => b.GetMembers().Any(static m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }));

        static IEnumerable<ITypeSymbol> BaseTypesOf(ITypeSymbol t)
        {
            for (var current = t; current is not null; current = current.BaseType)
                yield return current;
        }
    }

    enum TypeKind
    {
        OpenGenericPreProcessor,
        OpenGenericPostProcessor,
        Endpoint
    }
}
