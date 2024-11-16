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
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string INoRequest = "FastEndpoints.INoRequest";
    const string IEnumerable = "System.Collections.IEnumerable";
    const string DontRegisterAttribute = "DontRegisterAttribute";
    const string DontInjectAttribute = "DontInjectAttribute";
    const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    const string ConditionArgument = "Condition";

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
                       : type.AllInterfaces.Any(i => i.ToDisplayString() == IEndpoint) && //must be an endpoint
                         !type.AllInterfaces.Any(i => i.ToDisplayString() == INoRequest)  //must have a request dto
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

        b.Clear().w(
            $$"""
              #nullable enable

              using FastEndpoints;
              using System.Runtime.CompilerServices;

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
                          cache.TryAdd(
                              typeof({{tInfo!.Value.UnderlyingTypeName}}),
                              new()
                              {
                  """);

            if (!tInfo.Value.SkipObjectFactory)
            {
                b.w(
                    $"""
                     
                                     ObjectFactory = () => new {tInfo.Value.UnderlyingTypeName}({BuildCtorArgs(tInfo.Value.CtorArgumentCount)}){BuildInitializerArgs(tInfo.Value.RequiredProps)},
                     """);
            }

            b.w(
                """
                
                                Properties = new(
                                [
                """);

            foreach (var prop in tInfo.Value.Properties)
            {
                b.w(
                    $"""
                     
                                          new(typeof({tInfo.Value.UnderlyingTypeName}).GetProperty("{prop.PropName}")!,
                     """);
                b.w(
                    prop.IsInitOnly
                        ? " new()"
                        : $" new() {{ Setter = (dto, val) => {BuildPropCast(tInfo.Value)}.{prop.PropName} = ({prop.PropertyType})val! }}");
                b.w("),");
            }

            b.w(
                """
                
                                ])
                            });

                """);
        }

        b.w(
            """
                    return cache;
                }
            }
            """);

        TypeInfo.AllTypes.Clear();

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
                   ? $"Unsafe.Unbox<{t.UnderlyingTypeName}>(dto)"
                   : $"(({t.UnderlyingTypeName})dto)";
    }

    readonly struct TypeInfo
    {
        internal static HashSet<TypeInfo?> AllTypes { get; } = new(TypeNameComparer.Instance);

        public int HashCode { get; }
        public string? TypeName { get; }
        public string? UnderlyingTypeName { get; }
        public bool IsValueType { get; }
        public List<Prop> Properties { get; } = [];
        public bool SkipObjectFactory { get; }
        public int CtorArgumentCount { get; }
        public IEnumerable<string> RequiredProps => Properties.Where(p => p.IsRequired).Select(p => p.PropName);

        public TypeInfo(ITypeSymbol symbol, bool isEndpoint, bool noRecursion = false)
        {
            if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                return;

            ITypeSymbol? type = null;

            if (isEndpoint) //descend in to base types and find the request dto type
            {
                var tBase = symbol.BaseType;

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

            if (AllTypes.Contains(this)) //need to have TypeName set before this
                return;

            if (type.AllInterfaces.Any(i => i.ToDisplayString() == IEnumerable))
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

            var currentSymbol = type;
            var ctorSearchComplete = false;

            while (currentSymbol is not null)
            {
                foreach (var member in currentSymbol.GetMembers())
                {
                    switch (member)
                    {
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

            if (Properties.Count == 0)
                return;

            if (type.DeclaringSyntaxReferences.Length > 0)
                HashCode = type.DeclaringSyntaxReferences[0].Span.Length;

            if (isEndpoint is false && noRecursion) //treating an endpoint as a regular class to generate its props for property injection support
                SkipObjectFactory = true;

            AllTypes.Add(this);

            if (!noRecursion)
            {
                foreach (var p in Properties)
                    _ = new TypeInfo(p.Symbol, false);
            }

            if (isEndpoint) //create entry for endpoint class props as well to support property injection
                _ = new TypeInfo(symbol: symbol, isEndpoint: false, noRecursion: true); //process the endpoint as a regular class without recursion
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

            return x.Value.TypeName?.Equals(y.Value.TypeName) is true;
        }

        public int GetHashCode(TypeInfo? obj)
            => obj is null
                   ? 0
                   : obj.Value.TypeName?.GetHashCode() ?? 0;
    }

    class FullTypeComparer : IEqualityComparer<TypeInfo?>
    {
        internal static FullTypeComparer Instance { get; } = new();

        FullTypeComparer() { }

        public bool Equals(TypeInfo? x, TypeInfo? y)
        {
            if (x is null || y is null)
                return false;

            return x.Value.HashCode.Equals(y.Value.HashCode);
        }

        public int GetHashCode(TypeInfo? obj)
            => obj is null
                   ? 0
                   : obj.Value.HashCode;
    }
}