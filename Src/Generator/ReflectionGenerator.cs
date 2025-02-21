using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ReflectionGenerator : IIncrementalGenerator
{
    // ReSharper disable InconsistentNaming

    static readonly StringBuilder b = new();
    static readonly StringBuilder _initArgsBuilder = new();
    static readonly string[] TypeBlacklist = ["Microsoft.Extensions.Primitives.StringSegment", "FastEndpoints.EmptyRequest", "System.Uri"];
    const string ConditionArgument = "Condition";
    const string DontInjectAttribute = "DontInjectAttribute";
    const string DontRegisterAttribute = "DontRegisterAttribute";
    const string FluentGenericEp = "FastEndpoints.Ep.";
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string IEnumerable = "System.Collections.IEnumerable";
    const string IParsable = "System.IParsable<";
    const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    // ReSharper restore InconsistentNaming

    static string? _assemblyName;

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var syntaxProvider = ctx.SyntaxProvider
                                .CreateSyntaxProvider(Qualify, Transform)
                                .Where(static t => t is not null)
                                .WithComparer(FullTypeComparer.Instance)
                                .Collect();

        ctx.RegisterSourceOutput(syntaxProvider, Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static TypeInfo? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            return ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type ||
                   type.IsAbstract ||
                   type.GetAttributes().Any(a => a.AttributeClass!.Name == DontRegisterAttribute || type.AllInterfaces.Length == 0)
                       ? null
                       : type.AllInterfaces.Any(i => i.ToDisplayString() == IEndpoint) //must be an endpoint
                           ? new TypeInfo(type, true)
                           : null;
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, ImmutableArray<TypeInfo?> _)
    {
        var fileContent = RenderClass();
        spc.AddSource("ReflectionData.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    static string RenderClass()
    {
        _assemblyName ??= "Assembly"; //when no endpoints are present

        var sanitizedAssemblyName = _assemblyName.Sanitize(string.Empty);

        b.Clear();
        b.w(
            """
            #nullable enable

            using FastEndpoints;
            using System.Globalization;
            using System.Runtime.CompilerServices;


            """);

        foreach (var tInfo in TypeInfo.AllTypes)
        {
            b.w(
                $"""
                 using {tInfo!.TypeAlias} = {tInfo.UnderlyingTypeName};

                 """);
        }

        b.w(
            $$"""

              namespace {{_assemblyName}};

              /// <summary>
              /// source generated reflection data for request dtos located in the [{{_assemblyName}}] assembly.
              /// </summary>
              public static class GeneratedReflection
              {
                  /// <summary>
                  /// register source generated reflection data from [{{sanitizedAssemblyName}}] with the central cache.
                  /// </summary>
                  public static ReflectionCache AddFrom{{sanitizedAssemblyName}}(this ReflectionCache cache)
                  {

              """);

        foreach (var tInfo in TypeInfo.AllTypes)
        {
            b.w(
                $$"""
                          // {{tInfo!.UnderlyingTypeName}}
                          var _{{tInfo.TypeAlias}} = typeof({{tInfo.TypeAlias}});
                          cache.TryAdd(
                              _{{tInfo.TypeAlias}},
                              new()
                              {
                  """);

            if (!tInfo.SkipObjectFactory)
            {
                b.w(
                    $"""
                     
                                     ObjectFactory = () => new {tInfo.TypeAlias}({BuildCtorArgs(tInfo.CtorArgumentCount)}){BuildInitializerArgs(tInfo.RequiredProps)},
                     """);
            }

            if (tInfo.IsParsable is not null)
            {
                b.w(
                    $"""
                     
                                     ValueParser = input => new({tInfo.TypeAlias}.TryParse({BuildTryParseArgs(tInfo.IsParsable)}), result),
                     """);
            }

            if (tInfo.Properties.Count > 0)
            {
                b.w(
                    """
                    
                                    Properties = new(
                                    [
                    """);

                foreach (var prop in tInfo.Properties)
                {
                    b.w(
                        $"""
                         
                                              new(_{tInfo.TypeAlias}.GetProperty(nameof({tInfo.TypeAlias}.{prop.PropName}))!,
                         """);
                    b.w(
                        prop.IsInitOnly
                            ? " new()"
                            : $" new() {{ Setter = (dto, val) => {BuildPropCast(tInfo)}.{prop.PropName} = ({prop.PropertyType})val! }}");
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

        TypeInfo.Reset();

        return b.ToString();

        static string BuildCtorArgs(int argCount)
            => argCount == 0
                   ? string.Empty
                   : string.Join(", ", Enumerable.Repeat("default!", argCount));

        static string BuildInitializerArgs(IEnumerable<string> props)
        {
            if (!props.Any())
                return string.Empty;

            _initArgsBuilder.Clear();
            _initArgsBuilder.w(" { ");

            foreach (var p in props)
                _initArgsBuilder.w(p).w(" = default!, ");

            _initArgsBuilder.Remove(_initArgsBuilder.Length - 2, 2);
            _initArgsBuilder.w(" }");

            return _initArgsBuilder.ToString();
        }

        static string BuildPropCast(TypeInfo t)
            => t.IsValueType
                   ? $"Unsafe.Unbox<{t.TypeAlias}>(dto)"
                   : $"(({t.TypeAlias})dto)";

        static string BuildTryParseArgs(bool? isIParsable)
            => isIParsable is true
                   ? "input, CultureInfo.InvariantCulture, out var result"
                   : "input, out var result";
    }

    sealed class TypeInfo
    {
        internal static HashSet<TypeInfo?> AllTypes { get; } = new(TypeNameComparer.Instance);
        static int Counter { get; set; }

        public int HashCode { get; }
        public string? TypeAlias { get; }
        public string? TypeName { get; }
        public string? UnderlyingTypeName { get; }
        public bool IsValueType { get; }
        public List<Prop> Properties { get; } = [];
        public bool SkipObjectFactory { get; }
        public bool? IsParsable { get; }
        public int CtorArgumentCount { get; }
        public IEnumerable<string> RequiredProps => Properties.Where(p => p.IsRequired).Select(p => p.PropName);

        public TypeInfo(ITypeSymbol symbol, bool isEndpoint, bool noRecursion = false)
        {
            if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Enum || symbol.TypeKind == TypeKind.Interface)
                return;

            symbol = symbol.GetUnderlyingType();

            ITypeSymbol? type = null;

            if (isEndpoint) //descend in to base types and find the request dto type
            {
                var tBase = symbol.BaseType;

                if (tBase?.ToDisplayString().StartsWith(FluentGenericEp) is true)
                    tBase = tBase.BaseType;

                while (type is null)
                {
                    if (tBase?.TypeArguments.Length == 0)
                    {
                        tBase = tBase.BaseType;

                        continue;
                    }
                    type = tBase!.TypeArguments[0];
                }
            }
            else
                type = symbol;

            TypeName = type.ToDisplayString(); //must be set before checking AllTypes below
            UnderlyingTypeName = type.ToDisplayString(NullableFlowState.None);
            IsValueType = type.IsValueType;

            if (TypeBlacklist.Contains(UnderlyingTypeName))
                return;

            if (AllTypes.Contains(this)) //need to have TypeName set before this
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
                        _ = new TypeInfo(tElement, false);

                    return;
                }

                if (ifcName.StartsWith(IParsable))
                    IsParsable = true;
            }

            var currentSymbol = type;
            var ctorSearchComplete = false;

            while (currentSymbol is not null)
            {
                foreach (var member in currentSymbol.GetMembers())
                {
                    switch (member)
                    {
                        case IMethodSymbol method when method.Name == "TryParse" &&
                                                       method.DeclaredAccessibility == Accessibility.Public &&
                                                       method.IsStatic &&
                                                       method.ReturnType is { SpecialType: SpecialType.System_Boolean } &&
                                                       method.Parameters is { Length: 2 } args &&
                                                       args[0] is { Type.SpecialType: SpecialType.System_String } &&
                                                       args[1] is { RefKind: RefKind.Out, Type.Name: var outTypeName } &&
                                                       outTypeName == currentSymbol.Name:
                            IsParsable = false;

                            break;

                        case IMethodSymbol { MethodKind : MethodKind.Constructor, DeclaredAccessibility : Accessibility.Public, IsStatic : false } method:

                            if (!ctorSearchComplete)
                            {
                                var argCount = method.Parameters.Count(p => !p.HasExplicitDefaultValue);
                                if (CtorArgumentCount == 0 || (argCount > 0 && CtorArgumentCount > argCount))
                                    CtorArgumentCount = argCount;
                            }

                            break;

                        case IPropertySymbol
                        {
                            DeclaredAccessibility: Accessibility.Public,
                            IsStatic: false,
                            GetMethod.DeclaredAccessibility : Accessibility.Public,
                            SetMethod.DeclaredAccessibility: Accessibility.Public
                        } prop:
                            if (HasDontInjectAttribute(prop) ||            //ignore props with [DontInject] or
                                HasUnconditionalJsonIgnoreAttribute(prop)) //[JsonIgnore] or [JsonIgnore(Condition=Always)]
                                break;

                            Properties.Add(new(prop));

                            break;
                    }
                }
                ctorSearchComplete = true;
                currentSymbol = currentSymbol.BaseType;
            }

            if (type.DeclaringSyntaxReferences.Length > 0)
                HashCode = type.DeclaringSyntaxReferences[0].Span.Length;

            if (isEndpoint is false && noRecursion) //treating an endpoint as a regular class to generate its props for property injection support
                SkipObjectFactory = true;

            if (SkipObjectFactory is false && Properties.Count == 0)
                SkipObjectFactory = true;

            if (Properties.Count > 0)
            {
                TypeAlias = $"t{(Counter++).ToString()}";
                AllTypes.Add(this);

                if (!noRecursion)
                {
                    foreach (var p in Properties)
                        _ = new TypeInfo(p.Symbol, false);
                }
            }

            if (isEndpoint && Properties.Count > 0)                                     //create entry for endpoint class to support property injection
                _ = new TypeInfo(symbol: symbol, isEndpoint: false, noRecursion: true); //process the endpoint as a regular class without recursion
            else
            {
                if (noRecursion) // noRecursion is only true for endpoint classes when treated as regular classes by if condition above
                    return;

                TypeAlias = $"t{(Counter++).ToString()}";
                AllTypes.Add(this);
            }
        }

        internal static void Reset()
        {
            AllTypes.Clear();
            Counter = 0;
        }

        static bool HasDontInjectAttribute(IPropertySymbol prop)
            => prop.GetAttributes().Any(a => a.AttributeClass!.Name == DontInjectAttribute);

        static bool HasUnconditionalJsonIgnoreAttribute(IPropertySymbol prop)
        {
            foreach (var attribute in prop.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != JsonIgnoreAttribute)
                    continue;

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key != ConditionArgument)
                        continue;

                    var conditionValue = (int)namedArgument.Value.Value!;

                    if (conditionValue == 1) //Always
                        return true;
                }

                return true; // no condition, just plain [JsonIgnore]
            }

            return false;
        }

        internal readonly struct Prop(IPropertySymbol prop)
        {
            public ITypeSymbol Symbol { get; } = prop.Type;
            public string PropName { get; } = prop.Name;
            public string PropertyType { get; } = prop.Type.ToDisplayString();
            public bool IsInitOnly { get; } = prop.SetMethod?.IsInitOnly is true;
            public bool IsRequired { get; } = prop.IsRequired;
        }
    }

    class TypeNameComparer : IEqualityComparer<TypeInfo?>
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

    class FullTypeComparer : IEqualityComparer<TypeInfo?>
    {
        internal static FullTypeComparer Instance { get; } = new();

        FullTypeComparer() { }

        public bool Equals(TypeInfo? x, TypeInfo? y)
        {
            if (x is null || y is null)
                return false;

            return x.HashCode.Equals(y.HashCode);
        }

        public int GetHashCode(TypeInfo? obj)
            => obj is null
                   ? 0
                   : obj.HashCode;
    }
}