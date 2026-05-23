using System.Collections.Immutable;
using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class AccessControlGeneratorTests
{
    [Fact]
    public void generated_allow_code_compiles_without_implicit_usings()
    {
        var runResult = RunGenerator(out var outputCompilation, out var generatorDiagnostics);

        generatorDiagnostics.ShouldBeEmpty();
        runResult.GeneratedTrees.Select(t => Path.GetFileName(t.FilePath)).ShouldContain("Allow.b.g.cs");
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
    }

    [Fact]
    public void generated_permission_code_matches_runtime_acl_hash()
    {
        var runResult = RunGenerator(out _, out _);
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        allowSource.ShouldContain($"public const string Users_Create = {SymbolDisplay.FormatLiteral(HashEndpoint.Hash("Users+Create"), true)};");
    }

    [Fact]
    public void generated_identifiers_are_valid_for_digit_and_keyword_names()
    {
        var runResult = RunGenerator(out var outputCompilation, out var generatorDiagnostics, "AccessControl(\"2FA+Enable\", \"2FA\", \"class\");");
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        allowSource.ShouldContain($"public const string _2FA_Enable = {SymbolDisplay.FormatLiteral(HashEndpoint.Hash("2FA_Enable"), true)};");
        allowSource.ShouldContain("public static IEnumerable<string> _2FA");
        allowSource.ShouldContain("private static void AddTo_2FA");
        allowSource.ShouldContain("public static IEnumerable<string> _class");
        allowSource.ShouldContain("private static void AddTo_class");
    }

    static GeneratorDriverRunResult RunGenerator(
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        string accessControlStatement = "AccessControl(\"Users+Create\");")
    {
        var source =
            $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : Endpoint<EmptyRequest>
            {
                public override void Configure()
                {
                    {{accessControlStatement}}
                }

                public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
                    => Task.CompletedTask;
            }
            """;

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            "TestApp",
            [syntaxTree],
            GetReferences(),
            new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([new AccessControlGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out generatorDiagnostics);

        return driver.GetRunResult();
    }

    static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);

        foreach (var path in trustedPlatformAssemblies)
            yield return MetadataReference.CreateFromFile(path);

        yield return MetadataReference.CreateFromFile(typeof(Endpoint<>).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(HideFromDocsAttribute).Assembly.Location);
    }

    sealed class HashEndpoint : Endpoint<EmptyRequest>
    {
        public static string Hash(string input)
            => GetAclHash(input);

        public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
            => Task.CompletedTask;
    }
}
