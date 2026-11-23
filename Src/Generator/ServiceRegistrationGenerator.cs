using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    const string AttribMetadataName = "FastEndpoints.RegisterServiceAttribute`1";

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var provider = initCtx.SyntaxProvider
                              .ForAttributeWithMetadataName(AttribMetadataName, static (n, _) => !n.IsNestedInGenericType(), Transform)
                              .Where(static r => r is not null)
                              .Collect();

        initCtx.RegisterSourceOutput(provider.Combine(initCtx.RootNamespaceProvider()), Generate);
    }

    //executed only for declarations decorated with the attribute
    static Registration? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } implType)
            return null;

        //the attribute has AllowMultiple=false, so the first one is the only valid one
        var attrib = ctx.Attributes.FirstOrDefault();

        if (attrib?.AttributeClass is not { TypeArguments.Length: 1 } attribClass ||
            attrib.ConstructorArguments.Length != 1 ||
            attrib.ConstructorArguments[0] is not { Type: INamedTypeSymbol lifeTimeEnum, Value: { } lifeTimeValue })
            return null;

        var serviceType = attribClass.TypeArguments[0];

        var lifeTime = lifeTimeEnum.GetMembers()
                                   .OfType<IFieldSymbol>()
                                   .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, lifeTimeValue))
                                   ?.Name;

        return lifeTime is null
                   ? null
                   : new(serviceType.ToFullName(), implType.ToFullName(), lifeTime);
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext ctx, (ImmutableArray<Registration?> Registrations, string RootNamespace) input)
    {
        if (input.Registrations.Length == 0)
            return;

        var b = new StringBuilder().w(
            $$"""
              using Microsoft.Extensions.DependencyInjection;

              namespace {{input.RootNamespace}};

              public static class ServiceRegistrationExtensions
              {
                  public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection RegisterServicesFrom{{input.RootNamespace.ToValidIdentifier(string.Empty)}}(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection sc)
                  {

              """);

        foreach (var reg in input.Registrations
                                 .Select(static r => r!)
                                 .OrderBy(static r => r.LifeTime, StringComparer.Ordinal)
                                 .ThenBy(static r => r.ServiceType, StringComparer.Ordinal))
        {
            b.w(
                $"""
                         sc.Add{reg.LifeTime}<{reg.ServiceType}, {reg.ImplType}>();

                 """);
        }
        b.w(
            """

                    return sc;
                }
            }
            """);

        ctx.AddSource("ServiceRegistrations.g.cs", SourceText.From(b.ToString(), Encoding.UTF8));
    }

    sealed record Registration(string ServiceType, string ImplType, string LifeTime);
}
