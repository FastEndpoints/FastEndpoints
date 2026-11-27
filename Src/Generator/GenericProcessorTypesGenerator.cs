using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class GenericProcessorTypesGenerator : IIncrementalGenerator
{
    static readonly SymbolDisplayFormat _openGenericNameFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None);

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var provider = initCtx.SyntaxProvider
                              .CreateSyntaxProvider(Qualify, Transform)
                              .Where(static m => m is not null)
                              .Collect();

        //endpoint dtos are only needed to close open generic processors, which most projects don't have. the compilation is passed on only when
        //a processor exists, so that other projects don't inspect the dtos of every endpoint on each edit.
        var processorCompilation = initCtx.CompilationProvider
                                          .Combine(provider.Select(static (types, _) => types.Any(static t => t is ProcessorData)))
                                          .Select(static (x, _) => x.Right ? x.Left : null);

        initCtx.RegisterSourceOutput(provider.Combine(processorCompilation).Combine(initCtx.RootNamespaceProvider()), Generate);

        //executed per each keystroke - filter to classes and records that can implement an interface or derive from an endpoint base
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node.IsCandidateTypeDeclaration();
    }

    //executed per each keystroke but only for syntax nodes filtered by the Qualify method
    static TypeData? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        //IsRegistrable() is checked after a match, because binding attributes costs more than the checks that reject most types
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol type)
            return null;

        if (type.IsGenericType)
        {
            foreach (var ifc in type.AllInterfaces)
            {
                // IPreProcessor<TRequest>
                if (type.TypeParameters.Length == 1 && ifc.HasMetadataName(Extensions.IPreProcessor))
                    return Processor(isPostProcessor: false);

                // IPostProcessor<TRequest, TResponse>
                if (type.TypeParameters.Length == 2 && ifc.HasMetadataName(Extensions.IPostProcessor))
                    return Processor(isPostProcessor: true);
            }

            return null; //open generic endpoints are not discovered, so their type parameters must not be emitted
        }

        // Endpoint<TRequest, TResponse>
        return type.GetEndpointBase() is not null && type.IsRegistrable()
                   ? new EndpointData(type.MetadataFullName())
                   : null;

        ProcessorData? Processor(bool isPostProcessor)
            => type.IsRegistrable()
                   ? new(isPostProcessor, type.ToDisplayString(_openGenericNameFormat), new(type.TypeParameters.Select(Constraints.From).ToArray()))
                   : null;
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn.
    //while a processor exists, the input includes the compilation, so it's executed on each edit. without processors, the compilation is null.
    static void Generate(SourceProductionContext ctx, ((ImmutableArray<TypeData?>, Compilation?), string) input)
    {
        var ((types, compilation), rootNamespace) = input;

        ctx.AddSource(
            "GenericProcessorTypes.g.cs",
            SourceText.From(
                Extensions.RenderPreservedTypeList(rootNamespace, "GenericProcessorTypes", compilation is null ? [] : ClosedProcessors(types, compilation)),
                Encoding.UTF8));
    }

    //the type expressions of the open generic processors closed with the dtos of each endpoint
    static List<string> ClosedProcessors(ImmutableArray<TypeData?> types, Compilation compilation)
    {
        var preProcessors = ProcessorsOf(isPostProcessor: false);
        var postProcessors = ProcessorsOf(isPostProcessor: true);

        //only processors with constraint types or a new() constraint, which are rare, need a dto's assignable types or default constructor
        var constraints = preProcessors.Concat(postProcessors).SelectMany(static p => p.Constraints).ToArray();
        var needsAssignability = constraints.Any(static c => c.Types.Length > 0);
        var needsDefaultCtor = constraints.Any(static c => c.New);
        var dtos = new Dictionary<ITypeSymbol, Dto>(SymbolEqualityComparer.Default); //endpoints often share dtos
        var endpointDtos = new List<(Dto Request, Dto Response)>();

        //the endpoints are looked up in the source assembly, so types with the same metadata name in referenced assemblies can't interfere
        foreach (var endpoint in types.OfType<EndpointData>().Distinct())
        {
            if (compilation.Assembly.GetTypeByMetadataName(endpoint.MetadataName)?.GetEndpointBase() is { } epBase)
                endpointDtos.Add((DtoOf(epBase.TypeArguments[0]), DtoOf(epBase.TypeArguments[1])));
        }

        //processors are closed at runtime with the request/response types of the endpoint they're attached to
        var requestTypes = endpointDtos.Select(static e => e.Request)
                                       .Distinct()
                                       .OrderBy(static t => t.Name, StringComparer.Ordinal)
                                       .ToArray();
        var dtoPairs = endpointDtos.Distinct()
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

        return typeExpressions;

        ProcessorData[] ProcessorsOf(bool isPostProcessor)
            => types.OfType<ProcessorData>()
                    .Where(p => p.IsPostProcessor == isPostProcessor)
                    .Distinct()
                    .OrderBy(static t => t.Name, StringComparer.Ordinal)
                    .ToArray();

        Dto DtoOf(ITypeSymbol type)
        {
            if (!dtos.TryGetValue(type, out var dto))
                dtos[type] = dto = Dto.From(type, needsDefaultCtor, needsAssignability);

            return dto;
        }
    }

    abstract record TypeData;

    sealed record ProcessorData(bool IsPostProcessor, string Name, EquatableArray<Constraints> Constraints) : TypeData;

    //only the metadata name is stored, so that the per-edit transform doesn't inspect dtos that are only needed when processors exist.
    //ClosedProcessors() looks the endpoint up by it.
    sealed record EndpointData(string MetadataName) : TypeData;

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

    //HasDefaultCtor and AssignableTo (fully qualified names of the base types, including itself, and interfaces) are only computed when needed
    sealed record Dto(string Name, bool IsReferenceType, bool IsValueType, bool IsUnmanagedType, bool HasDefaultCtor, EquatableArray<string> AssignableTo)
    {
        internal static Dto From(ITypeSymbol t, bool needsDefaultCtor, bool needsAssignability)
        {
            var isValueType = t.IsValueType && t.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

            return new(
                t.ToFullName(),
                t.IsReferenceType,
                isValueType,
                isValueType && t.IsUnmanagedType,
                needsDefaultCtor && CanConstructWithNew(t),
                needsAssignability ? new(t.BaseTypesAndSelf().Concat(t.AllInterfaces).Select(static b => b.ToFullName()).ToArray()) : default);
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

            return ctor is not null && !ctor.UnsetRequiredMembers().Any();
        }
    }
}
