using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ReflectionGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming
    static readonly string[] TypeBlacklist = ["Microsoft.Extensions.Primitives.StringSegment", "FastEndpoints.EmptyRequest", "System.Uri"];
    const string ConditionArgument = "Condition";
    const string DontInjectAttribute = "DontInjectAttribute";
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string IEnumerable = "System.Collections.IEnumerable";
    const string IParsable = "System.IParsable<";
    const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    const string KeyedServiceAttribute = "FastEndpoints.KeyedServiceAttribute";
    const string ReflectionCache = "FastEndpoints.ReflectionCache";

    static readonly SymbolDisplayFormat _propertyTypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    // ReSharper restore InconsistentNaming

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var syntaxProvider = initCtx.SyntaxProvider
                                    .CreateSyntaxProvider(Qualify, Transform)
                                    .Where(static t => t.Length > 0)
                                    .Collect();

        var assemblyNamespace = initCtx.CompilationProvider.Select(
            static (c, _) => c.GetTypeByMetadataName(ReflectionCache) is null
                                 ? null
                                 : c.ToRootNamespace());

        initCtx.RegisterSourceOutput(syntaxProvider.Combine(assemblyNamespace), Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null, BaseList: not null } && !node.IsNestedInGenericType();

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method.
        //each endpoint gets its own set of collected types so that the cached result only depends on the endpoint and the types it references.
        static EquatableArray<TypeModel> Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol { IsAbstract: false } type ||
                type.HasDontRegisterAttribute() ||
                !type.AllInterfaces.Any(static i => i.ToDisplayString() == IEndpoint)) //must be an endpoint
                return default;

            return new(TypeCollector.CollectEndpoint(type));
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, (ImmutableArray<EquatableArray<TypeModel>> Endpoints, string? RootNamespace) input)
    {
        // Messaging-only consumers have no HTTP reflection cache to register with.
        if (input.RootNamespace is null)
            return;

        //a dto shared by multiple endpoints yields the same model for each of them
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        var types = input.Endpoints.SelectMany(static e => e).Where(t => typeNames.Add(t.TypeName)).ToArray();

        spc.AddSource("ReflectionData.g.cs", SourceText.From(RenderClass(input.RootNamespace, types), Encoding.UTF8));
    }

    static string RenderClass(string rootNamespace, TypeModel[] types)
    {
        var b = new StringBuilder().w(
            """
            #pragma warning disable CS0618
            #nullable enable


            """);

        //property types are aliased once instead of being repeated in every typeof() and setter cast. dto aliases are reused and keyword types stay inline.
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < types.Length; i++)
            AddAlias(types[i].UnderlyingTypeName, Alias(i));

        //must run after the dto aliases so that dto types used as property types reuse them
        var propAliasCount = 0;

        foreach (var propType in types.SelectMany(static t => t.Properties).Select(static p => p.Type))
        {
            if (!aliases.ContainsKey(propType) && propType.Contains("global::"))
                AddAlias(propType, $"p{(propAliasCount++).ToString()}");
        }

        b.w(
            $$"""

              namespace {{rootNamespace}};

              /// <summary>
              /// source generated reflection data for request dtos located in the [{{rootNamespace}}] assembly.
              /// </summary>
              public static class GeneratedReflection
              {
                  /// <summary>
                  /// register source generated reflection data from [{{rootNamespace}}] with the central cache.
                  /// </summary>
                  public static global::FastEndpoints.ReflectionCache AddFrom{{rootNamespace.ToValidIdentifier(string.Empty)}}(this global::FastEndpoints.ReflectionCache cache)
                  {

              """);

        for (var i = 0; i < types.Length; i++)
        {
            var tInfo = types[i];
            var alias = Alias(i);

            b.w(
                $$"""
                          // {{tInfo.UnderlyingTypeName}}
                          var _{{alias}} = typeof({{alias}});
                          cache.TryAdd(
                              _{{alias}},
                              new()
                              {
                  """);

            if (!tInfo.SkipObjectFactory)
            {
                b.w(
                    $"""

                                     ObjectFactory = () => new {alias}({BuildCtorArgs(tInfo.CtorArgumentTypes)}){BuildInitializerArgs(tInfo.RequiredMembers)},
                     """);
            }

            if (tInfo.IsParsable is not null)
            {
                b.w(
                    $"""

                                     ValueParser = input => new({alias}.TryParse({BuildTryParseArgs(tInfo.IsParsable)}), result),
                     """);
            }

            if (tInfo.Properties.Length > 0)
            {
                b.w(
                    """

                                    Properties = new(
                                    [
                    """);

                foreach (var prop in tInfo.Properties)
                {
                    var propName = prop.Name.EscapeKeyword();
                    var getter = $"Getter = dto => {BuildPropCast(tInfo, alias)}.{propName}";
                    var propType = aliases.TryGetValue(prop.Type, out var propAlias) ? propAlias : prop.Type;

                    b.w(
                        $"""

                                              new(_{alias}.GetProperty(nameof({alias}.{propName}), typeof({propType}))!,
                         """);
                    b.w(
                        prop.IsInitOnly
                            ? $" new() {{ {getter}{BuildServiceKey(prop)} }}"
                            : $" new() {{ {getter}, Setter = (dto, val) => {BuildPropCast(tInfo, alias)}.{propName} = ({propType})val!{BuildServiceKey(prop)} }}");
                    b.w("),");
                }

                b.w(
                    """

                                    ])
                    """);
            }

            b.w(
                """

                            });

                """);
        }

        b.w(
            """
                    return cache;
                }
            }
            """);

        return b.ToString();

        //indexer instead of Add(), since dtos that differ only in nullable annotations can share an underlying type name
        void AddAlias(string typeName, string alias)
        {
            aliases[typeName] = alias;
            b.w(
                $"""
                 using {alias} = {typeName};

                 """);
        }

        static string Alias(int index)
            => $"t{index.ToString()}";

        //typed defaults pick the intended overload when several constructors have the same number of required parameters
        static string BuildCtorArgs(EquatableArray<string> argTypes)
            => string.Join(", ", argTypes.Select(static t => $"default({t})!"));

        static string BuildInitializerArgs(EquatableArray<string> requiredMembers)
            => requiredMembers.Length == 0
                   ? string.Empty
                   : $" {{ {string.Join(", ", requiredMembers.Select(static name => $"{name.EscapeKeyword()} = default!"))} }}";

        static string BuildPropCast(TypeModel t, string alias)
            => t.IsValueType
                   ? $"global::System.Runtime.CompilerServices.Unsafe.Unbox<{alias}>(dto)"
                   : $"(({alias})dto)";

        static string BuildServiceKey(PropModel p)
            => p.ServiceKey is null ? string.Empty : $", ServiceKey = {SymbolDisplay.FormatLiteral(p.ServiceKey, quote: true)}";

        static string BuildTryParseArgs(bool? isIParsable)
            => isIParsable is true
                   ? "input, global::System.Globalization.CultureInfo.InvariantCulture, out var result"
                   : "input, out var result";
    }

    sealed record TypeModel(string TypeName,
                            string UnderlyingTypeName,
                            bool IsValueType,
                            bool SkipObjectFactory,
                            bool? IsParsable,
                            EquatableArray<string> CtorArgumentTypes,
                            EquatableArray<string> RequiredMembers,
                            EquatableArray<PropModel> Properties);

    sealed record PropModel(string Name, string Type, bool IsInitOnly, string? ServiceKey);

    static class TypeCollector
    {
        //the request dto graph is collected before the endpoint class
        internal static TypeModel[] CollectEndpoint(ITypeSymbol endpoint)
        {
            var output = new List<TypeModel>();
            var registered = new HashSet<string>(StringComparer.Ordinal);

            //EndpointWithoutRequest etc. resolve to the blacklisted EmptyRequest
            if (endpoint.GetEndpointBase()?.TypeArguments[0] is { } requestDto)
                Collect(output, registered, requestDto, CollectMode.RequestDto);

            Collect(output, registered, endpoint, CollectMode.EndpointProperties);

            return [.. output];
        }

        //only registered types short-circuit, so a request dto without props is still registered when it's later reached as a nested type.
        //a type is registered after its own member walk and before its property types are walked, which decides the output (and alias) order.
        static void Collect(List<TypeModel> output, HashSet<string> registered, ITypeSymbol type, CollectMode mode)
        {
            //the request dto is walked as declared, so abstract and interface dtos are still registered and a nullable type argument keeps its annotation
            if (mode != CollectMode.RequestDto)
            {
                if (type.IsAbstract || type.TypeKind is TypeKind.Enum or TypeKind.Interface or TypeKind.Dynamic) //dynamic is object at runtime and typeof(dynamic) doesn't compile
                    return;

                type = type.GetUnderlyingType();
            }

            var typeName = type.ToDisplayString();

            if (TypeBlacklist.Contains(type.ToDisplayString(NullableFlowState.None)) || registered.Contains(typeName))
                return;

            bool? isParsable = null;

            foreach (var ifcName in type.AllInterfaces.Select(ifc => ifc.ToDisplayString()))
            {
                if (ifcName == IEnumerable)
                {
                    var tElement = type switch
                    {
                        IArrayTypeSymbol arrayType => arrayType.ElementType,
                        INamedTypeSymbol { TypeArguments.Length: > 0 } namedType => namedType.TypeArguments[0],
                        _ => null
                    };

                    if (tElement is not null)
                        Collect(output, registered, tElement, CollectMode.Nested);

                    return;
                }

                if (ifcName.StartsWith(IParsable, StringComparison.Ordinal))
                    isParsable = true;
            }

            var properties = new List<PropModel>();
            var propTypes = new List<ITypeSymbol>();
            var propNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var currentSymbol in type.BaseTypesAndSelf())
            {
                foreach (var member in currentSymbol.GetMembers())
                {
                    switch (member)
                    {
                        // ReSharper disable MergeIntoPattern
                        case IMethodSymbol method when method.Name == "TryParse" &&
                                                       method.DeclaredAccessibility == Accessibility.Public &&
                                                       method.IsStatic &&
                                                       method.ReturnType is { SpecialType: SpecialType.System_Boolean } &&
                                                       method.Parameters is { Length: 2 } args &&
                                                       args[0] is { Type.SpecialType: SpecialType.System_String } &&
                                                       args[1] is { RefKind: RefKind.Out, Type.Name: var outTypeName } &&
                                                       outTypeName == currentSymbol.Name: // ReSharper enable MergeIntoPattern
                            isParsable = false;

                            break;

                        case IPropertySymbol { IsStatic: false, IsIndexer: false } prop:
                            if (prop.DeclaredAccessibility != Accessibility.Public)
                                break;

                            //an overridden or hidden property is represented by the most derived declaration, same as with runtime reflection
                            if (!propNames.Add(prop.Name))
                                break;

                            if (prop.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                                prop.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                                break;

                            //ignore props with [DontInject] or [JsonIgnore] or [JsonIgnore(Condition=Always)]
                            if (HasDontInjectAttribute(prop) || HasUnconditionalJsonIgnoreAttribute(prop))
                                break;

                            properties.Add(
                                new(
                                    prop.Name,
                                    PropertyTypeOf(prop.Type),
                                    prop.SetMethod?.IsInitOnly is true,
                                    mode == CollectMode.EndpointProperties ? ServiceKeyOf(prop) : null));
                            propTypes.Add(prop.Type);

                            break;
                    }
                }
            }

            var ctor = PickConstructor(type);
            var skipObjectFactory = mode == CollectMode.EndpointProperties || //the endpoint class is only walked for property injection support
                                    properties.Count == 0 ||
                                    type.IsAbstract ||
                                    IsPartial(type) ||
                                    ctor is null; //no public constructor to call

            //optional and params parameters are omitted so that they keep their declared default values (or an empty params collection)
            var ctorArgTypes = ctor?.Parameters.Where(static p => !p.HasExplicitDefaultValue && !p.IsParams).Select(static p => p.Type.ToFullName()).ToArray() ?? [];
            string[] requiredMembers = skipObjectFactory || ctor!.SetsRequiredMembers() ? [] : [.. type.RequiredMemberNames()];

            //the request dto and the endpoint class are only registered when they have props
            if (properties.Count > 0 || mode == CollectMode.Nested)
            {
                registered.Add(typeName);
                output.Add(
                    new(
                        typeName,
                        type.ToDisplayString(NullableFlowState.None, SymbolDisplayFormat.FullyQualifiedFormat),
                        type.IsValueType,
                        skipObjectFactory,
                        isParsable,
                        new(ctorArgTypes),
                        new(requiredMembers),
                        new([.. properties])));
            }

            if (mode == CollectMode.EndpointProperties) //injected services aren't bound, so their types aren't walked
                return;

            foreach (var propType in propTypes)
                Collect(output, registered, propType, CollectMode.Nested);
        }

        //same pick as the runtime binder: the public constructor with the fewest parameters, the first declared one on a tie
        static IMethodSymbol? PickConstructor(ITypeSymbol type)
        {
            IMethodSymbol? pick = null;

            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Constructor, DeclaredAccessibility: Accessibility.Public, IsStatic: false } ctor &&
                    (pick is null || ctor.Parameters.Length < pick.Parameters.Length))
                    pick = ctor;
            }

            return pick;
        }

        enum CollectMode
        {
            RequestDto,
            Nested,
            EndpointProperties
        }

        //used for the GetProperty() lookup, the setter cast and type aliases, which reject a top-level nullable reference annotation.
        //nested annotations are kept, otherwise casting to List<string> and assigning to a List<string?> property warns.
        //a top-level dynamic is emitted as object (its runtime type), since typeof(dynamic) doesn't compile. nested dynamic compiles via aliases.
        static string PropertyTypeOf(ITypeSymbol type)
            => type.TypeKind == TypeKind.Dynamic
                   ? "object"
                   : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(_propertyTypeFormat);

        static string? ServiceKeyOf(IPropertySymbol prop)
            => AttributesOf(prop)
               .FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == KeyedServiceAttribute)?.ConstructorArguments
               .FirstOrDefault().Value as string;

        //attributes of overridden properties are inherited at runtime (Attribute.GetCustomAttribute/IsDefined), derived ones first
        static IEnumerable<AttributeData> AttributesOf(IPropertySymbol prop)
        {
            for (var p = prop; p is not null; p = p.OverriddenProperty)
            {
                foreach (var attribute in p.GetAttributes())
                    yield return attribute;
            }
        }

        static bool HasDontInjectAttribute(IPropertySymbol prop)
            => AttributesOf(prop).Any(static a => a.AttributeClass?.Name == DontInjectAttribute);

        static bool IsPartial(ITypeSymbol type)
            => type.DeclaringSyntaxReferences.Any(
                static r => r.GetSyntax() is TypeDeclarationSyntax t &&
                            t.Modifiers.Any(static m => m.ValueText == "partial"));

        //mirrors the runtime BindableProps() check, which only skips JsonIgnoreCondition.Always (the default condition)
        static bool HasUnconditionalJsonIgnoreAttribute(IPropertySymbol prop)
        {
            foreach (var attribute in AttributesOf(prop))
            {
                if (attribute.AttributeClass?.ToDisplayString() != JsonIgnoreAttribute)
                    continue;

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == ConditionArgument)
                        return namedArgument.Value.Value is 1; //Always
                }

                return true; // no condition, just plain [JsonIgnore]
            }

            return false;
        }
    }
}