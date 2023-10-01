using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private static readonly StringBuilder b = new();
    private static string? _assemblyName;
    private const string _attribShortName = "RegisterService";
    private const string _attribMetadataName = "RegisterServiceAttribute`1";

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
                cds.AttributeLists.Any(
                    al => al.Attributes.Any(
                        a => a.Name is GenericNameSyntax { Identifier.ValueText: _attribShortName }));
        }

        static Registration? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var service = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node);

            if (service?.IsAbstract is null or true)
                return null;

            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            var svcType = service
                .GetAttributes()
                .Single(a => a.AttributeClass!.MetadataName == _attribMetadataName)
                .AttributeClass!
                .TypeArguments[0]
                .ToDisplayString();

            var implType = service.ToDisplayString();

            var attrib = (ctx.Node as ClassDeclarationSyntax)!
                .AttributeLists
                .SelectMany(al => al.Attributes)
                .First(a => ((GenericNameSyntax)a.Name).Identifier.ValueText == _attribShortName);
            var arg = (MemberAccessExpressionSyntax)attrib
                .ArgumentList!
                .Arguments
                .OfType<AttributeArgumentSyntax>()
                .Single()
                .Expression;
            var lifetime = ((IdentifierNameSyntax)arg.Name).Identifier.ValueText;

            return new(svcType, implType, lifetime);
        }
    }

    private static void Generate(SourceProductionContext ctx, ImmutableArray<Registration> regs)
    {
        if (!regs.Any())
            return;

        b.Clear().w(
"namespace ").w(_assemblyName).w(@";

using Microsoft.Extensions.DependencyInjection;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterServicesFrom").w(_assemblyName?.Sanitize(string.Empty) ?? "Assembly").w(@"(this IServiceCollection sc)
    {
");
        foreach (var reg in regs.OrderBy(r => r!.LifeTime).ThenBy(r => r!.ServiceType))
        {
            b.w(
"        sc.Add").w(reg!.LifeTime).w("<").w(reg.ServiceType).w(",").w(" ").w(reg.ImplType).w(@">();
");
        }
        b.w(@"
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

            unchecked
            {
                Hash = 17;
                Hash = (Hash * 23) + svcType.GetHashCode();
                Hash = (Hash * 23) + implType.GetHashCode();
                Hash = (Hash * 23) + life.GetHashCode();
            }
        }

        public bool Equals(Registration other) => other.Hash.Equals(Hash);
    }
}
