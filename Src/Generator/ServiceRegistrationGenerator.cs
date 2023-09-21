using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string attribShortName = "RegisterService";
    private const string attribMetadataName = "RegisterServiceAttribute`1";

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var provider = ctx.SyntaxProvider
            .CreateSyntaxProvider(Match, Transform)
            .Where(r => r is not null)
            .Collect();

        ctx.RegisterSourceOutput(provider, Generate);
    }

    private bool Match(SyntaxNode node, CancellationToken _)
    {
        return node is ClassDeclarationSyntax cds &&
               cds.AttributeLists.Count > 0 &&
               cds.AttributeLists.Any(
                   static al => al.Attributes.Any(
                       static a => a.Name is GenericNameSyntax { Identifier.ValueText: attribShortName }));
    }

    private static string? _assemblyName;

    private static Registration? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        var service = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as ITypeSymbol;

        if (service?.IsAbstract is null or true)
            return null;

        _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

        var svcType = service
            .GetAttributes()
            .Single(a => a.AttributeClass!.MetadataName == attribMetadataName)
            .AttributeClass!
            .TypeArguments[0]
            .ToDisplayString();

        var implType = service.ToDisplayString();

        var attrib = (ctx.Node as ClassDeclarationSyntax)!
            .AttributeLists
            .SelectMany(al => al.Attributes)
            .First(a => ((GenericNameSyntax)a.Name).Identifier.ValueText == attribShortName);
        var arg = (MemberAccessExpressionSyntax)attrib
            .ArgumentList!
            .Arguments
            .OfType<AttributeArgumentSyntax>()
            .Single()
            .Expression;
        var lifetime = ((IdentifierNameSyntax)arg.Name).Identifier.ValueText;

        return new(svcType, implType, lifetime);
    }

    private static readonly StringBuilder b = new();
    private static void Generate(SourceProductionContext ctx, ImmutableArray<Registration?> regs)
    {
        if (!regs.Any())
            return;

        b.Clear().w(
"namespace ").w(_assemblyName).w(@";

using Microsoft.Extensions.DependencyInjection;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterServicesFrom").w(_assemblyName).w(@"(this IServiceCollection sc)
    {
");
        foreach (var reg in regs.OrderBy(r => r!.LifeTime).ThenBy(r => r!.ServiceType))
        {
            b.w(
"        sc.Add").w(reg!.LifeTime).w("<").w(reg.ServiceType).w(",").w(" ").w(reg.ImplType).w(">();").AppendLine();
        }
        b.Remove(b.Length - 2, 2).w(@"
        return sc;
    }
}");

        ctx.AddSource("ServiceRegistrations.g.cs", SourceText.From(b.ToString(), Encoding.UTF8));
    }

    private sealed class Registration : IEquatable<Registration>
    {
        public int Hash { get; }
        public string ServiceType { get; }
        public string ImplType { get; }
        public string LifeTime { get; }

        public Registration(string svcType, string implType, string life)
        {
            ServiceType = svcType;
            ImplType = implType;
            LifeTime = life;
            Hash = CalculateHash(svcType, implType, life);
        }

        private static int CalculateHash(string svcType, string implType, string life)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + svcType.GetHashCode();
                hash = (hash * 23) + implType.GetHashCode();
                hash = (hash * 23) + life.GetHashCode();
                return hash;
            }
        }

        public bool Equals(Registration other) => other.Hash.Equals(Hash);
    }
}
