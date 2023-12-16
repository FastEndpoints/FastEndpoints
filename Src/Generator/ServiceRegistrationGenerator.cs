using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    // ReSharper disable once InconsistentNaming
    static readonly StringBuilder b = new();
    static string? _assemblyName;
    const string AttribShortName = "RegisterService";
    const string AttribMetadataName = "RegisterServiceAttribute`1";

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var provider = ctx.SyntaxProvider
                          .CreateSyntaxProvider(Match, Transform)
                          .Where(static r => r is not null)
                          .Collect();

        ctx.RegisterSourceOutput(provider, Generate!);

        static bool Match(SyntaxNode node, CancellationToken _)
        {
            return
                node is ClassDeclarationSyntax cds &&
                cds.TypeParameterList is null &&
                cds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name is GenericNameSyntax { Identifier.ValueText: AttribShortName }));
        }

        static Registration? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var service = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node);

            if (service?.IsAbstract is null or true)
                return null;

            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            var svcType = service
                          .GetAttributes()
                          .Single(a => a.AttributeClass!.MetadataName == AttribMetadataName)
                          .AttributeClass!
                          .TypeArguments[0]
                          .ToDisplayString();

            var implType = service.ToDisplayString();

            var attrib = (ctx.Node as ClassDeclarationSyntax)!
                         .AttributeLists
                         .SelectMany(al => al.Attributes)
                         .First(a => ((GenericNameSyntax)a.Name).Identifier.ValueText == AttribShortName);
            var arg = (MemberAccessExpressionSyntax)
                attrib
                    .ArgumentList!
                    .Arguments
                    .Single()
                    .Expression;
            var lifetime = ((IdentifierNameSyntax)arg.Name).Identifier.ValueText;

            return new(svcType, implType, lifetime);
        }
    }

    static void Generate(SourceProductionContext ctx, ImmutableArray<Registration> regs)
    {
        if (!regs.Any())
            return;

        b.Clear().w(
            $$"""
              namespace {{_assemblyName}};

              using Microsoft.Extensions.DependencyInjection;

              public static class ServiceRegistrationExtensions
              {
                  public static IServiceCollection RegisterServicesFrom{{_assemblyName?.Sanitize(string.Empty) ?? "Assembly"}}(this IServiceCollection sc)
                  {

              """);

        foreach (var reg in regs.OrderBy(r => r!.LifeTime).ThenBy(r => r!.ServiceType))
        {
            b.w(
                $"""
                         sc.Add{reg!.LifeTime}<{reg.ServiceType}, {reg.ImplType}>();

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

    sealed class Registration : IEquatable<Registration>
    {
        public string ServiceType { get; }
        public string ImplType { get; }
        public string LifeTime { get; }

        readonly int _hash;

        public Registration(string svcType, string implType, string life)
        {
            ServiceType = svcType;
            ImplType = implType;
            LifeTime = life;

            unchecked
            {
                _hash = 17;
                _hash = _hash * 23 + svcType.GetHashCode();
                _hash = _hash * 23 + implType.GetHashCode();
                _hash = _hash * 23 + life.GetHashCode();
            }
        }

        public bool Equals(Registration other)
            => other._hash.Equals(_hash);
    }
}