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
    const string SetsRequiredMembersAttribute = "global::System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

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

            var collected = new HashSet<TypeInfo?>(TypeNameComparer.Instance);
            TypeInfo.CollectEndpoint(collected, type);

            return new([.. collected.Select(static t => t!.ToModel())]);
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

    sealed class TypeInfo
    {
        public string? TypeName { get; }
        string? UnderlyingTypeName { get; }
        bool IsValueType { get; }
        List<Prop> Properties { get; } = [];
        List<string> RequiredMembers { get; } = [];
        bool SkipObjectFactory { get; }
        bool? IsParsable { get; }
        string[] CtorArgumentTypes { get; } = [];

        //the request dto graph is collected before the endpoint class
        internal static void CollectEndpoint(HashSet<TypeInfo?> collected, ITypeSymbol endpoint)
        {
            //EndpointWithoutRequest etc. resolve to the blacklisted EmptyRequest
            if (endpoint.GetEndpointBase()?.TypeArguments[0] is { } requestDto)
                _ = new TypeInfo(collected, requestDto, CollectMode.RequestDto);

            _ = new TypeInfo(collected, endpoint, CollectMode.EndpointProperties);
        }

        TypeInfo(ISet<TypeInfo?> collected, ITypeSymbol type, CollectMode mode)
        {
            //the request dto is walked as declared, so abstract and interface dtos are still registered and a nullable type argument keeps its annotation
            if (mode != CollectMode.RequestDto)
            {
                if (type.IsAbstract || type.TypeKind is TypeKind.Enum or TypeKind.Interface or TypeKind.Dynamic) //dynamic is object at runtime and typeof(dynamic) doesn't compile
                    return;

                type = type.GetUnderlyingType();
            }

            TypeName = type.ToDisplayString(); //must be set before checking the collected types below
            UnderlyingTypeName = type.ToDisplayString(NullableFlowState.None, SymbolDisplayFormat.FullyQualifiedFormat);
            IsValueType = type.IsValueType;

            if (TypeBlacklist.Contains(type.ToDisplayString(NullableFlowState.None)) || collected.Contains(this))
                return;

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
                        _ = new TypeInfo(collected, tElement, CollectMode.Nested);

                    return;
                }

                if (ifcName.StartsWith(IParsable, StringComparison.Ordinal))
                    IsParsable = true;
            }

            var currentSymbol = type;
            var ctorSearchComplete = false;
            int? ctorParamCount = null;
            string[]? ctorArgTypes = null;
            var ctorSetsRequiredMembers = false;
            var propNames = new HashSet<string>(StringComparer.Ordinal);
            var requiredNames = new HashSet<string>(StringComparer.Ordinal);

            while (currentSymbol is not null)
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
                            IsParsable = false;

                            break;

                        //same pick as the runtime binder: the public constructor with the fewest parameters.
                        //optional and params parameters are omitted so that they keep their declared default values (or an empty params collection).
                        case IMethodSymbol { MethodKind : MethodKind.Constructor, DeclaredAccessibility : Accessibility.Public, IsStatic : false } method
                            when !ctorSearchComplete:

                            if (ctorParamCount is null || method.Parameters.Length < ctorParamCount)
                            {
                                ctorParamCount = method.Parameters.Length;
                                ctorArgTypes = method.Parameters.Where(static p => !p.HasExplicitDefaultValue && !p.IsParams).Select(static p => p.Type.ToFullName()).ToArray();
                                ctorSetsRequiredMembers = method.GetAttributes().Any(static a => a.AttributeClass?.ToFullName() == SetsRequiredMembersAttribute);
                            }

                            break;

                        case IFieldSymbol { IsStatic: false, IsRequired: true } field:
                            if (requiredNames.Add(field.Name))
                                RequiredMembers.Add(field.Name);

                            break;

                        case IPropertySymbol { IsStatic: false, IsIndexer: false } prop:
                            if (prop.IsRequired && requiredNames.Add(prop.Name))
                                RequiredMembers.Add(prop.Name);

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

                            Properties.Add(new(prop, mode == CollectMode.EndpointProperties));

                            break;
                    }
                }
                ctorSearchComplete = true;
                currentSymbol = currentSymbol.BaseType;
            }

            if (ctorArgTypes is not null)
                CtorArgumentTypes = ctorArgTypes;

            if (mode == CollectMode.EndpointProperties) //the endpoint class is treated as a regular class to generate its props for property injection support
                SkipObjectFactory = true;

            if (!SkipObjectFactory && Properties.Count == 0)
                SkipObjectFactory = true;

            if (type.IsAbstract || IsPartial(type) || ctorArgTypes is null) //no public constructor to call
                SkipObjectFactory = true;

            if (SkipObjectFactory || ctorSetsRequiredMembers)
                RequiredMembers.Clear();

            //the request dto and the endpoint class are only registered when they have props
            if (Properties.Count > 0 || mode == CollectMode.Nested)
                collected.Add(this);

            if (mode == CollectMode.EndpointProperties) //injected services aren't bound, so their types aren't walked
                return;

            foreach (var p in Properties)
                _ = new TypeInfo(collected, p.Symbol, CollectMode.Nested);
        }

        internal TypeModel ToModel()
            => new(
                TypeName!,
                UnderlyingTypeName!,
                IsValueType,
                SkipObjectFactory,
                IsParsable,
                new(CtorArgumentTypes),
                new(RequiredMembers.ToArray()),
                new(Properties.Select(static p => new PropModel(p.PropName, p.PropertyType, p.IsInitOnly, p.ServiceKey)).ToArray()));

        enum CollectMode
        {
            RequestDto,
            Nested,
            EndpointProperties
        }

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

        readonly struct Prop(IPropertySymbol prop, bool isEndpointClass = false)
        {
            public ITypeSymbol Symbol { get; } = prop.Type;
            public string PropName { get; } = prop.Name;

            //used for the GetProperty() lookup, the setter cast and type aliases, which reject a top-level nullable reference annotation.
            //nested annotations are kept, otherwise casting to List<string> and assigning to a List<string?> property warns.
            //a top-level dynamic is emitted as object (its runtime type), since typeof(dynamic) doesn't compile. nested dynamic compiles via aliases.
            public string PropertyType { get; } = prop.Type.TypeKind == TypeKind.Dynamic
                                                      ? "object"
                                                      : prop.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(_propertyTypeFormat);

            public bool IsInitOnly { get; } = prop.SetMethod?.IsInitOnly is true;

            public string? ServiceKey { get; } = isEndpointClass
                                                     ? AttributesOf(prop)
                                                       .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == KeyedServiceAttribute)?.ConstructorArguments
                                                       .FirstOrDefault().Value as string
                                                     : null;
        }
    }

    sealed class TypeNameComparer : IEqualityComparer<TypeInfo?>
    {
        internal static TypeNameComparer Instance { get; } = new();

        TypeNameComparer() { }

        public bool Equals(TypeInfo? x, TypeInfo? y)
        {
            if (x is null || y is null)
                return false;

            return x.TypeName?.Equals(y.TypeName) is true;
        }

        public int GetHashCode(TypeInfo? obj)
            => obj is null
                   ? 0
                   : obj.TypeName?.GetHashCode() ?? 0;
    }
}