using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class AccessControlGeneratorTests
{
    static readonly ImmutableArray<MetadataReference> _references = GeneratorTestHarness.GetReferences(typeof(Endpoint<>).Assembly, typeof(HideFromDocsAttribute).Assembly);

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
    public void acl_hash_is_not_affected_by_the_current_culture()
    {
        var inputs = Enumerable.Range(0, 200).Select(i => $"Perm{i}").ToArray();
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new("tr-TR");

            foreach (var input in inputs)
                HashEndpoint.Hash(input).ShouldMatch("^[A-Z0-9]{3}$");

            // the base64 hash of "Perm29" starts with "ij4", so a culture-sensitive generator would emit "İJ4"
            var runResult = RunGenerator(out _, out _, "AccessControl(\"Perm29\");");
            var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

            allowSource.ShouldContain($"public const string Perm29 = {SymbolDisplay.FormatLiteral(HashEndpoint.Hash("Perm29"), true)};");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    //codes are persisted by users (claims, databases), so they must never change for a given permission name.
    //the expected values come from an independent implementation of the algorithm.
    [Theory]
    [InlineData("Users+Create", "H7M")]
    [InlineData("Café+Ünïcode--Perm", "SVN")]
    [InlineData("Foo__Bar", "LK1")]
    [InlineData("  lead trail  ", "VAY")]
    [InlineData("日本語", "0UK")]
    public void acl_hash_codes_are_stable(string permissionName, string expectedCode)
    {
        HashEndpoint.Hash(permissionName).ShouldBe(expectedCode);

        var runResult = RunGenerator(out _, out _, $"AccessControl({SymbolDisplay.FormatLiteral(permissionName, true)});");
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        allowSource.ShouldContain($" = \"{expectedCode}\";");
    }

    [Fact]
    public void partial_property_code_matches_the_access_control_code_for_the_same_name()
    {
        //names that sanitizing changes, so a partial property must sanitize like AccessControl("...") does
        const string allowSource =
            """
            namespace TestApp.Auth;

            public static partial class Allow
            {
                public static partial string Foo__Bar { get; }
                public static partial string Café { get; }
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain($"public static partial string Foo__Bar {{ get => \"{HashEndpoint.Hash("Foo__Bar")}\"; }}");
        result.Generated.ShouldContain($"public static partial string Café {{ get => \"{HashEndpoint.Hash("Café")}\"; }}");
        HashEndpoint.Hash("Foo__Bar").ShouldBe("LK1");
        HashEndpoint.Hash("Café").ShouldBe("RWL");
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
        var initial = GeneratorTestHarness.Run<AccessControlGenerator>(
            [EndpointSource, ConstantsSource("Admin")],
            _references,
            hintFileName: "Allow.g.cs");

        var updatedCompilation = initial.Input.ReplaceSyntaxTree(
            initial.Input.SyntaxTrees.ElementAt(1),
            CSharpSyntaxTree.ParseText(ConstantsSource("Manager"), GeneratorTestHarness.DefaultParseOptions));
        var updated = GeneratorTestHarness.Step(initial.Driver, updatedCompilation, "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(initial);
        GeneratorTestHarness.AssertCompiles(updated);
        initial.Generated.ShouldContain("public static IEnumerable<string> Admin => _admin");
        updated.Generated.ShouldContain("public static IEnumerable<string> Manager => _manager");
        updated.Generated.ShouldNotContain("public static IEnumerable<string> Admin => _admin");

        static string ConstantsSource(string groupName)
            => $$"""
                 namespace TestApp;

                 static class PermissionGroup
                 {
                     internal const string Group = "{{groupName}}";
                 }
                 """;
    }

    [Fact]
    public void access_control_nested_in_a_block_is_generated()
    {
        var runResult = RunGenerator(out var outputCompilation, out var generatorDiagnostics, "if (true) { AccessControl(\"Nested_Perm\"); }");
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        allowSource.ShouldContain($"public const string Nested_Perm = {SymbolDisplay.FormatLiteral(HashEndpoint.Hash("Nested_Perm"), true)};");
    }

    [Theory]
    [InlineData("this")]
    [InlineData("base")]
    public void qualified_access_control_call_is_generated(string receiver)
    {
        var runResult = RunGenerator(
            out var outputCompilation,
            out var generatorDiagnostics,
            $"{receiver}.AccessControl(\"Qualified_Perm\", Apply.ToThisEndpoint, \"Admin\");",
            """
            static class Consumer
            {
                static string Code => TestApp.Auth.Allow.Qualified_Perm;
            }
            """);
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        allowSource.ShouldContain($"public const string Qualified_Perm = {SymbolDisplay.FormatLiteral(HashEndpoint.Hash("Qualified_Perm"), true)};");
        Regex.IsMatch(allowSource, @"_admin = new\(\)\s*\{\s*Qualified_Perm\s*\};").ShouldBeTrue();
    }

    [Fact]
    public void same_named_user_method_is_ignored()
    {
        var runResult = RunGenerator(
            out var outputCompilation,
            out var generatorDiagnostics,
            additionalSource:
            """
            class NotAnEndpoint
            {
                void Configure()
                {
                    AccessControl("Not_A_Perm");
                    this.AccessControl("Not_A_Perm");
                }

                void AccessControl(string name) { }
            }
            """);
        var allowSource = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs").GetText().ToString();

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        allowSource.ShouldContain("public const string Users_Create");
        allowSource.ShouldNotContain("Not_A_Perm");
    }

    [Fact]
    public void description_is_xml_escaped_and_endpoint_cref_resolves()
    {
        var runResult = RunGenerator(out var outputCompilation, out _, "AccessControl( //admins <only> & owners\n \"Described_Perm\");");
        var allowTree = runResult.GeneratedTrees.Single(t => Path.GetFileName(t.FilePath) == "Allow.g.cs");

        allowTree.GetText().ToString().ShouldContain("<summary>admins &lt;only&gt; &amp; owners</summary>");
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Location.SourceTree == allowTree && d.Id is "CS1570" or "CS1574" or "CS1584")
                         .ShouldBeEmpty();
    }

    [Fact]
    public void assembly_name_starting_with_a_digit_yields_valid_namespace()
    {
        var runResult = RunGenerator(out var outputCompilation, out var generatorDiagnostics, assemblyName: "2FA.event-Api.class");

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        runResult.GeneratedTrees
                 .Single(t => Path.GetFileName(t.FilePath) == "Allow.b.g.cs")
                 .GetText()
                 .ToString()
                 .ShouldContain("namespace _2FA.event_Api._class.Auth;");
    }

    [Fact]
    public void custom_fields_in_any_allow_part_and_spelling_are_included_in_lookups()
    {
        const string allowSource =
            """
            using System;
            using FastEndpoints;

            namespace TestApp.Auth;

            public partial class Allow
            {
                public const string NoStatic_Part = "000";
            }

            public static partial class Allow
            {
                public const String Pascal_String = "001";
                public const System.String Qualified_String = "002";
                public const string Multi_A = "003", Multi_B = "004";
                public static readonly string Static_Field = "005";
                public const int Not_A_String = 6;

                [HideFromDocs]
                public const string Hidden = "007";
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);

        using var stream = new MemoryStream();
        result.Output.Emit(stream).Success.ShouldBeTrue();
        var allow = Assembly.Load(stream.ToArray()).GetType("TestApp.Auth.Allow", throwOnError: true)!;
        var names = ((IEnumerable<string>)allow.GetMethod("AllNames")!.Invoke(null, null)!).ToArray();
        var codes = ((IEnumerable<string>)allow.GetMethod("AllCodes")!.Invoke(null, null)!).ToArray();

        names.ShouldBe(
            ["Inventory_Update_Item", "NoStatic_Part", "Pascal_String", "Qualified_String", "Multi_A", "Multi_B", "Static_Field"],
            ignoreOrder: true);
        codes.ShouldContain("000");
        codes.ShouldContain("004");
        codes.ShouldContain("005");
        allow.GetMethod("PermissionCodeFor")!.Invoke(null, ["NoStatic_Part"]).ShouldBe("000");
    }

    [Fact]
    public void static_readonly_fields_with_keyword_or_parameter_names_are_included_in_lookups()
    {
        const string allowSource =
            """
            namespace TestApp.Auth;

            public static partial class Allow
            {
                public static readonly string @class = "x";
                public static readonly string permNames = "y";
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);

        using var stream = new MemoryStream();
        result.Output.Emit(stream).Success.ShouldBeTrue();
        var allow = Assembly.Load(stream.ToArray()).GetType("TestApp.Auth.Allow", throwOnError: true)!;
        var codeFor = allow.GetMethod("PermissionCodeFor")!;
        var nameFor = allow.GetMethod("PermissionNameFor")!;

        codeFor.Invoke(null, ["class"]).ShouldBe("x");
        codeFor.Invoke(null, ["permNames"]).ShouldBe("y");
        nameFor.Invoke(null, ["x"]).ShouldBe("class");
    }

    [Fact]
    public void partial_property_typed_as_string_alias_gets_implementation()
    {
        const string allowSource =
            """
            using System;

            namespace TestApp.Auth;

            public static partial class Allow
            {
                public static partial String Pascal_Partial { get; }
                public static partial System.String Qualified_Partial { get; }
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("public static partial string Pascal_Partial");
        result.Generated.ShouldContain("public static partial string Qualified_Partial");
    }

    [Fact]
    public void verbatim_keyword_partial_property_gets_implementation()
    {
        const string allowSource =
            """
            namespace TestApp.Auth;

            public static partial class Allow
            {
                public static partial string @class { get; }
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("public static partial string @class");
    }


    [Fact]
    public void lookups_list_endpoint_permissions_then_fields_then_partial_properties()
    {
        //the lookups are filled by indexer assignment, so this order decides which name wins on a code clash
        const string allowSource =
            """
            namespace TestApp.Auth;

            public static partial class Allow
            {
                public const string Z_Field = "ZZZ";
                public static partial string A_Partial { get; }
                public const string B_Field = "BBB";
            }
            """;

        var result = GeneratorTestHarness.Run<AccessControlGenerator>([EndpointSource, PermissionGroupSource, allowSource], _references, hintFileName: "Allow.g.cs");

        GeneratorTestHarness.AssertCompiles(result);
        string[] expectedOrder = ["Inventory_Update_Item", "B_Field", "Z_Field", "A_Partial"];
        var positions = expectedOrder.Select(n => result.Generated.IndexOf($"permNames[\"{n}\"]", StringComparison.Ordinal)).ToArray();
        positions.ShouldAllBe(p => p >= 0);
        positions.ShouldBe([.. positions.Order()]);
    }

    const string PermissionGroupSource = "namespace TestApp; static class PermissionGroup { internal const string Group = \"Admin\"; }";

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
        string additionalSource = "",
        string assemblyName = "TestApp")
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

        var result = GeneratorTestHarness.Run<AccessControlGenerator>(
            [source],
            _references,
            assemblyName,
            new(LanguageVersion.Preview, DocumentationMode.Diagnose));

        outputCompilation = result.Output;
        generatorDiagnostics = result.Diagnostics;

        return result.Driver.GetRunResult();
    }

    sealed class HashEndpoint : Endpoint<EmptyRequest>
    {
        public static string Hash(string input)
            => GetAclHash(input);

        public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
            => Task.CompletedTask;
    }
}
