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
                          .CreateSyntaxProvider(Qualify, Transform)
                          .Where(static m => m.IsInvalid is false)
                          .WithComparer(new Comparer())
                          .Collect();

        ctx.RegisterSourceOutput(provider, Generate);

        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null } cds &&
               cds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name is GenericNameSyntax { Identifier.ValueText: AttribShortName }));

        static Match Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            return new(ctx.SemanticModel.GetDeclaredSymbol(ctx.Node), (ClassDeclarationSyntax)ctx.Node);
        }
    }

    static void Generate(SourceProductionContext ctx, ImmutableArray<Match> matches)
    {
        if (matches.Length == 0)
            return;

        var regs = matches.Select(static m => new Registration(m));

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

    readonly struct Match(ISymbol? symbol, ClassDeclarationSyntax classDec)
    {
        public ISymbol? Symbol { get; } = symbol;
        public ClassDeclarationSyntax ClassDec { get; } = classDec;
        public bool IsInvalid => Symbol?.IsAbstract is null or true;
    }

    class Comparer : IEqualityComparer<Match>
    {
        public bool Equals(Match x, Match y)
            => x.Symbol!.ToDisplayString().Equals(y.Symbol!.ToDisplayString()) &&
               x.ClassDec.AttributeLists.ToString().Equals(y.ClassDec.AttributeLists.ToString());

        public int GetHashCode(Match obj)
            => obj.Symbol!.ToDisplayString().GetHashCode();
    }

    readonly struct Registration
    {
        public string ServiceType { get; }
        public string ImplType { get; }
        public string LifeTime { get; }

        public Registration(Match m)
        {
            ServiceType = m.Symbol!
                           .GetAttributes()
                           .Single(a => a.AttributeClass!.MetadataName == AttribMetadataName)
                           .AttributeClass!
                           .TypeArguments[0]
                           .ToDisplayString();

            ImplType = m.Symbol.ToDisplayString();

            var attrib = m.ClassDec
                          .AttributeLists
                          .SelectMany(al => al.Attributes)
                          .First(a => (a.Name as GenericNameSyntax)?.Identifier.ValueText == AttribShortName);

            var arg = (MemberAccessExpressionSyntax)
                attrib.ArgumentList!
                      .Arguments
                      .Single()
                      .Expression;

            LifeTime = ((IdentifierNameSyntax)arg.Name).Identifier.ValueText;
        }
    }
}