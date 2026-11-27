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
            => node is ClassDeclarationSyntax && node.IsCandidateTypeDeclaration();
    }

    //executed per each keystroke but only for syntax nodes filtered by the Qualify method
    static TypeData? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol type || !type.IsRegistrable())
            return null;

        if (type.IsGenericType)
        {
            foreach (var ifc in type.AllInterfaces)
            {
                var ifcName = ifc.ToDisplayString();

                // IPreProcessor<TRequest>
                if (type.TypeParameters.Length == 1 && ifcName.StartsWith(IPreProcessor, StringComparison.Ordinal))
                    return Processor(isPostProcessor: false);

                // IPostProcessor<TRequest, TResponse>
                if (type.TypeParameters.Length == 2 && ifcName.StartsWith(IPostProcessor, StringComparison.Ordinal))
                    return Processor(isPostProcessor: true);
            }

            return null; //open generic endpoints are not discovered, so their type parameters must not be emitted
        }

        // Endpoint<TRequest, TResponse>
        return type.GetEndpointBase() is { } epBase
                   ? new EndpointDtos(Dto.From(epBase.TypeArguments[0]), Dto.From(epBase.TypeArguments[1]))
                   : null;

        ProcessorData Processor(bool isPostProcessor)
            => new(isPostProcessor, type.ToDisplayString(_openGenericNameFormat), new(type.TypeParameters.Select(Constraints.From).ToArray()));
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext ctx, (ImmutableArray<TypeData?> Types, string RootNamespace) input)
    {
        var preProcessors = ProcessorsOf(isPostProcessor: false);
        var postProcessors = ProcessorsOf(isPostProcessor: true);

        var endpoints = input.Types.OfType<EndpointDtos>().ToArray();

        //processors are closed at runtime with the request/response types of the endpoint they're attached to
        var requestTypes = endpoints.Select(static e => e.Request)
                                    .Distinct()
                                    .OrderBy(static t => t.Name, StringComparer.Ordinal)
                                    .ToArray();
        var dtoPairs = endpoints.Select(static e => (e.Request, e.Response))
                                .Distinct()
                                .OrderBy(static p => p.Request.Name, StringComparer.Ordinal)
                                .ThenBy(static p => p.Response.Name, StringComparer.Ordinal)
                                .ToArray();

        var typeExpressions = new List<string>();

        foreach (var processor in preProcessors)
        {
            //a processor attached to an endpoint whose dto violates its constraints would fail at runtime anyway
            foreach (var reqType in requestTypes.Where(r => r.Satisfies(processor.Constraints[0], r)))
                typeExpressions.Add($"{processor.Name}<{reqType.Name}>");
        }

        foreach (var processor in postProcessors)
        {
            foreach (var (req, res) in dtoPairs.Where(p => p.Request.Satisfies(processor.Constraints[0], p.Request, p.Response) &&
                                                           p.Response.Satisfies(processor.Constraints[1], p.Request, p.Response)))
                typeExpressions.Add($"{processor.Name}<{req.Name}, {res.Name}>");
        }

        ctx.AddSource(
            "GenericProcessorTypes.g.cs",
            SourceText.From(Extensions.RenderPreservedTypeList(input.RootNamespace, "GenericProcessorTypes", typeExpressions), Encoding.UTF8));

        ProcessorData[] ProcessorsOf(bool isPostProcessor)
            => input.Types.OfType<ProcessorData>()
                    .Where(p => p.IsPostProcessor == isPostProcessor)
                    .Distinct()
                    .OrderBy(static t => t.Name, StringComparer.Ordinal)
                    .ToArray();
    }

    abstract record TypeData;

    sealed record ProcessorData(bool IsPostProcessor, string Name, EquatableArray<Constraints> Constraints) : TypeData;

    sealed record EndpointDtos(Dto Request, Dto Response) : TypeData;

    //the constraints of a processor's type parameter. constraint types are name templates in which the processor's own type parameters
    //are {ordinal} placeholders (braces can't occur in type names), so that constraints like IResult<TRequest> can be closed with dto types.
    sealed record Constraints(bool Class, bool Struct, bool Unmanaged, bool New, EquatableArray<string> Types)
    {
        internal static Constraints From(ITypeParameterSymbol p)
            => new(
                p.HasReferenceTypeConstraint,
                p.HasValueTypeConstraint,
                p.HasUnmanagedTypeConstraint,
                p.HasConstructorConstraint,
                new(p.ConstraintTypes.Select(ToTemplate).ToArray()));

        static string ToTemplate(ITypeSymbol t)
            => string.Concat(
                t.ToDisplayParts(SymbolDisplayFormat.FullyQualifiedFormat)
                 .Select(static part => part.Symbol is ITypeParameterSymbol tp ? $"{{{tp.Ordinal}}}" : part.ToString()));
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
                new(t.BaseTypesAndSelf().Concat(t.AllInterfaces).Select(static b => b.ToFullName()).ToArray()));
        }

        //typeArgs are the dtos the processor is closed with, in type parameter order
        internal bool Satisfies(Constraints c, params Dto[] typeArgs)
            => (!c.Class || IsReferenceType) &&
               (!c.Struct || IsValueType) &&
               (!c.Unmanaged || IsUnmanagedType) &&
               (!c.New || HasDefaultCtor) &&
               c.Types.All(t => AssignableTo.Contains(Close(t, typeArgs)));

        static string Close(string template, Dto[] typeArgs)
        {
            for (var i = 0; i < typeArgs.Length; i++)
                template = template.Replace($"{{{i}}}", typeArgs[i].Name);

            return template;
        }

        static bool CanConstructWithNew(ITypeSymbol t)
        {
            if (t.TypeKind == TypeKind.Enum)
                return true;

            if (t is not INamedTypeSymbol { IsAbstract: false } n)
                return false;

            var ctor = n.InstanceConstructors.FirstOrDefault(static c => c is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 0 });

            return ctor is not null && (ctor.SetsRequiredMembers() || !t.RequiredMemberNames().Any());
        }
    }
}
