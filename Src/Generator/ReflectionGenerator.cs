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
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string INoRequest = "FastEndpoints.INoRequest";
    const string IEnumerable = "System.Collections.IEnumerable";
    const string DontRegisterAttribute = "DontRegisterAttribute";
    const string JsonIgnoreAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    const string ConditionArgument = "Condition";

    // ReSharper restore InconsistentNaming

    static string? _assemblyName;

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var syntaxProvider = ctx.SyntaxProvider
                                .CreateSyntaxProvider(Qualify, Transform)
                                .Where(static t => t is not null)
                                .WithComparer(FullDtoComparer.Instance)
                                .Collect();

        ctx.RegisterSourceOutput(syntaxProvider, Generate!);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static DtoInfo? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            return ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type ||
                   type.IsAbstract ||
                   type.GetAttributes().Any(a => a.AttributeClass!.Name == DontRegisterAttribute || type.AllInterfaces.Length == 0)
                       ? null
                       : type.AllInterfaces.Any(i => i.ToDisplayString() == IEndpoint) && //must be an endpoint
                         !type.AllInterfaces.Any(i => i.ToDisplayString() == INoRequest)  //must have a request dto
                           ? GetDtoInfo(type)
                           : null;

            static DtoInfo? GetDtoInfo(ITypeSymbol tEndpoint)
            {
                var dto = new DtoInfo(tEndpoint);

                if (dto is { IsEnumerable: false, IsRecord : false, Properties.Count: > 0 })
                    return dto;

                return null;
            }
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, ImmutableArray<DtoInfo?> dtoInfos)
    {
        var fileContent = RenderClass(dtoInfos);
        spc.AddSource("ReflectionData.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    static string RenderClass(IEnumerable<DtoInfo?> dtos)
    {
        b.Clear().w(
            $$"""
              #nullable enable

              using System.Reflection;

              namespace {{_assemblyName}}
              {
                  public static class SourceGeneratedReflectionExtensions
                  {
                      static Dictionary<
                          Type,                         //key: type of the request dto
                          (                             //val: tuple of...
                          Dictionary<                   //     item1: props dictionary
                              PropertyInfo,             //            key: type/info of property
                              Action<object, object?>>, //            val: prop setter action (dto instance, prop value to set)
                          Func<object>                  //     item2: dto instance factory
                          )> _map { get; } = new()
                      {
              """);

        foreach (var dto in dtos.Distinct(DtoTypeNameComparer.Instance))
        {
            if (dto is null)
                continue;

            b.w(
                $$"""
                  
                              {
                                  typeof({{dto.Value.TypeName}}),
                                  (
                                      new()
                                      {
                  """);

            foreach (var prop in dto.Value.Properties!)
            {
                b.w(
                    $$"""
                      
                                              {
                                                  typeof({{dto.Value.TypeName}}).GetProperty("{{prop.PropName}}")!,

                      """);
                b.w(
                    prop.IsInitOnly
                        ? $"                            (_, _) => throw new NotSupportedException(\"Init properties are not supported with reflection source generation! Offender: [{dto.Value.TypeName}.{prop.PropName}]\")"
                        : $"                            (dto, val) => (({dto.Value.TypeName})dto).{prop.PropName} = ({prop.PropertyType})val!");
                b.w(
                    """
                    
                                            },
                    """);
            }

            b.w(
                $$"""
                  
                                      },
                                      () => new {{dto.Value.TypeName}}({{BuildCtorArgs(dto.Value.CtorArgumentCount)}})
                                  )
                              },
                  """);
        }

        var assemblyName = _assemblyName?.Sanitize(string.Empty) ?? "Assembly";

        b.w(
            $$"""
              
                      };
              
                      /// <summary>
                      /// register source generated reflection data from [{{assemblyName}}] with the central cache.
                      /// </summary>
                      public static void AddFrom{{assemblyName}}(this Dictionary<Type, (Dictionary<PropertyInfo, Action<object, object?>>, Func<object>)> cache)
                      {
                          foreach (var kvp in _map)
                              cache.TryAdd(kvp.Key, kvp.Value);
                      }
                  }
              }
              """);

        return b.ToString();

        static string BuildCtorArgs(int argCount)
            => argCount == 0
                   ? string.Empty
                   : string.Join(", ", Enumerable.Repeat("default!", argCount));
    }

    readonly struct DtoInfo
    {
        public int HashCode { get; }
        public string TypeName { get; }
        public List<Prop>? Properties { get; }
        public int CtorArgumentCount { get; }
        public bool IsEnumerable { get; }
        public bool IsRecord { get; }

        public DtoInfo(ITypeSymbol tEndpoint)
        {
            ITypeSymbol? tRequest = null;
            var tBase = tEndpoint.BaseType;

            while (tRequest is null)
            {
                if (tBase?.TypeArguments.Length == 0)
                {
                    tBase = tBase.BaseType;

                    continue;
                }
                tRequest = tBase!.TypeArguments[0];
            }

            TypeName = tRequest.ToDisplayString();

            if (tRequest.DeclaringSyntaxReferences.Length > 0)
                HashCode = tRequest.DeclaringSyntaxReferences[0].Span.Length;

            if (tRequest.IsRecord)
            {
                IsRecord = true;

                return;
            }

            if (tRequest.AllInterfaces.Any(i => i.ToDisplayString() == IEnumerable))
            {
                IsEnumerable = true;

                return;
            }

            var currentSymbol = tRequest;

            while (currentSymbol is not null)
            {
                foreach (var member in currentSymbol.GetMembers())
                {
                    switch (member)
                    {
                        case IMethodSymbol { MethodKind : MethodKind.Constructor, DeclaredAccessibility : Accessibility.Public, IsStatic : false } method:
                            var argCount = method.Parameters.Count(p => !p.HasExplicitDefaultValue);
                            if (CtorArgumentCount == 0 || (argCount > 0 && CtorArgumentCount > argCount))
                                CtorArgumentCount = argCount;

                            break;

                        case IPropertySymbol
                        {
                            DeclaredAccessibility: Accessibility.Public,
                            IsStatic: false,
                            GetMethod.DeclaredAccessibility : Accessibility.Public,
                            SetMethod.DeclaredAccessibility: Accessibility.Public
                        } prop:
                            if (HasUnconditionalJsonIgnoreAttribute(prop)) //[JsonIgnore] or [JsonIgnore(Condition=Always)]
                                break;

                            (Properties ??= []).Add(new(prop));

                            break;
                    }
                }
                currentSymbol = currentSymbol.BaseType;
            }
        }

        static bool HasUnconditionalJsonIgnoreAttribute(IPropertySymbol propertySymbol)
        {
            foreach (var attribute in propertySymbol.GetAttributes())
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
            public string PropName { get; } = prop.Name;
            public string PropertyType { get; } = prop.Type.ToDisplayString();
            public bool IsInitOnly { get; } = prop.SetMethod?.IsInitOnly is true;
        }
    }

    class DtoTypeNameComparer : IEqualityComparer<DtoInfo?>
    {
        internal static DtoTypeNameComparer Instance { get; } = new();

        DtoTypeNameComparer() { }

        public bool Equals(DtoInfo? x, DtoInfo? y)
        {
            if (x is null || y is null)
                return false;

            return x.Value.TypeName.Equals(y.Value.TypeName);
        }

        public int GetHashCode(DtoInfo? obj)
            => obj is null
                   ? 0
                   : obj.Value.TypeName.GetHashCode();
    }

    class FullDtoComparer : IEqualityComparer<DtoInfo?>
    {
        internal static FullDtoComparer Instance { get; } = new();

        FullDtoComparer() { }

        public bool Equals(DtoInfo? x, DtoInfo? y)
        {
            if (x is null || y is null)
                return false;

            return x.Value.HashCode.Equals(y.Value.HashCode);
        }

        public int GetHashCode(DtoInfo? obj)
            => obj is null
                   ? 0
                   : obj.Value.HashCode;
    }
}