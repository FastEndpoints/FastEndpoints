using System.Collections.Immutable;
using System.Text.RegularExpressions;
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

    [Fact]
    public void duplicate_generated_permission_codes_emit_diagnostic()
    {
        RunGenerator(
            out _,
            out var generatorDiagnostics,
            """
            AccessControl("Perm61");
            AccessControl("Perm217");
            """);

        var diagnostic = generatorDiagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FEAC001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("JCA");
        diagnostic.GetMessage().ShouldContain("Perm61");
        diagnostic.GetMessage().ShouldContain("Perm217");
    }

    [Fact]
    public void non_literal_const_and_nameof_categories_are_resolved()
    {
        var runResult = RunGenerator(
            out var outputCompilation,
            out var generatorDiagnostics,
            "AccessControl(\"Inventory_Update_Item\", Apply.ToThisEndpoint, new[] { PermissionGroup.Admin, nameof(PermissionGroup.Manager) });",
            """
            static class PermissionGroup
            {
                internal const string Admin = nameof(Admin);
                internal const string Manager = "Manager";
            }
            """);

        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        allowSource.ShouldContain("public const string Inventory_Update_Item");
        allowSource.ShouldContain("public static IEnumerable<string> Admin => _admin");
        allowSource.ShouldContain("public static IEnumerable<string> Manager => _manager");
        allowSource.ShouldContain("private static void AddToAdmin(string permissionCode)");
        allowSource.ShouldContain("private static void AddToManager(string permissionCode)");
        allowSource.ShouldContain("_admin = new()");
        allowSource.ShouldContain("_manager = new()");
        // permission is listed under both resolved category groups
        Regex.Matches(allowSource, @"(?m)^\s+Inventory_Update_Item\s*$").Count.ShouldBe(2);
    }

    [Fact]
    public void reordered_named_permission_argument_is_resolved()
    {
        var runResult = RunGenerator(
            out var outputCompilation,
            out var generatorDiagnostics,
            "AccessControl(behavior: Apply.ToThisEndpoint, keyName: \"Inventory_Update_Item\");");
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        allowSource.ShouldContain("public const string Inventory_Update_Item");
    }

    [Fact]
    public void unresolved_permission_name_does_not_promote_group_to_permission()
    {
        var runResult = RunGenerator(
            out var outputCompilation,
            out var generatorDiagnostics,
            "AccessControl(PermissionName.Runtime, PermissionGroup.Admin);",
            """
            static class PermissionName
            {
                internal static string Runtime => "Inventory_Update_Item";
            }

            static class PermissionGroup
            {
                internal const string Admin = nameof(Admin);
            }
            """);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        runResult.GeneratedTrees.Select(t => Path.GetFileName(t.FilePath)).ShouldNotContain("Allow.g.cs");
    }

    [Fact]
    public void referenced_constant_changes_invalidate_incremental_output()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var endpointTree = CSharpSyntaxTree.ParseText(EndpointSource, parseOptions);
        var initialConstantsTree = CSharpSyntaxTree.ParseText(ConstantsSource("Admin"), parseOptions);
        var compilation = CSharpCompilation.Create(
            "TestApp",
            [endpointTree, initialConstantsTree],
            GetReferences(),
            new(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new AccessControlGenerator().AsSourceGenerator()], parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var initialDiagnostics);
        var initialSource = driver.GetRunResult().GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        var updatedConstantsTree = CSharpSyntaxTree.ParseText(ConstantsSource("Manager"), parseOptions);
        var updatedCompilation = compilation.ReplaceSyntaxTree(initialConstantsTree, updatedConstantsTree);
        driver = driver.RunGeneratorsAndUpdateCompilation(updatedCompilation, out var outputCompilation, out var updatedDiagnostics);
        var updatedSource = driver.GetRunResult().GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        initialDiagnostics.ShouldBeEmpty();
        updatedDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        initialSource.ShouldContain("public static IEnumerable<string> Admin => _admin");
        updatedSource.ShouldContain("public static IEnumerable<string> Manager => _manager");
        updatedSource.ShouldNotContain("public static IEnumerable<string> Admin => _admin");

        static string ConstantsSource(string groupName)
            => $$"""
                 namespace TestApp;

                 static class PermissionGroup
                 {
                     internal const string Group = "{{groupName}}";
                 }
                 """;
    }

    const string EndpointSource =
        """
        using System.Threading;
        using System.Threading.Tasks;
        using FastEndpoints;

        namespace TestApp;

        public class MyEndpoint : Endpoint<EmptyRequest>
        {
            public override void Configure()
            {
                AccessControl("Inventory_Update_Item", PermissionGroup.Group);
            }

            public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
                => Task.CompletedTask;
        }
        """;

    static GeneratorDriverRunResult RunGenerator(
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        string accessControlStatement = "AccessControl(\"Users+Create\");",
        string additionalSource = "")
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

            {{additionalSource}}
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
