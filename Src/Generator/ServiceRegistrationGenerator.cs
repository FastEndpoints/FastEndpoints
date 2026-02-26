using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    const string AttribShortName = "RegisterService";
    const string AttribMetadataName = "RegisterServiceAttribute`1";

    // ReSharper disable once InconsistentNaming
    readonly StringBuilder b = new();
    string? _rootNamespace;

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var provider = initCtx.SyntaxProvider
                              .CreateSyntaxProvider(Qualify, Transform)
                              .Where(static m => m.IsInvalid is false)
                              .WithComparer(MatchComparer.Instance)
                              .Collect();

        initCtx.RegisterSourceOutput(provider, Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is ClassDeclarationSyntax { TypeParameterList: null } cds &&
               cds.AttributeLists.Any(al => al.Attributes.Any(a => a.Name is GenericNameSyntax { Identifier.ValueText: AttribShortName }));

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        Match Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            //should be re-assigned on every call. do not cache!
            _rootNamespace = ctx.SemanticModel.Compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly";

            return new(ctx.SemanticModel.GetDeclaredSymbol(ctx.Node), (ClassDeclarationSyntax)ctx.Node);
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    void Generate(SourceProductionContext ctx, ImmutableArray<Match> matches)
    {
        if (matches.Length == 0)
            return;

        var regs = matches.Select(static m => new Registration(m));

        b.Clear().w(
            $$"""
              namespace {{_rootNamespace}};

              using Microsoft.Extensions.DependencyInjection;

              public static class ServiceRegistrationExtensions
              {
                  public static IServiceCollection RegisterServicesFrom{{_rootNamespace!.ToValidIdentifier(string.Empty)}}(this IServiceCollection sc)
                  {

              """);

        foreach (var reg in regs.OrderBy(r => r.LifeTime).ThenBy(r => r.ServiceType))
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

    readonly struct Match(ISymbol? symbol, ClassDeclarationSyntax classDec)
    {
        public ISymbol? Symbol { get; } = symbol;
        public ClassDeclarationSyntax ClassDec { get; } = classDec;
        public bool IsInvalid => Symbol?.IsAbstract is null or true;
    }

    class MatchComparer : IEqualityComparer<Match>
    {
        internal static MatchComparer Instance { get; } = new();

        MatchComparer() { }

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