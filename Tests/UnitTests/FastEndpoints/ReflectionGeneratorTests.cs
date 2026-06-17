using System.Collections.Immutable;
using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class ReflectionGeneratorTests
{
    [Fact]
    public void request_dto_property_emits_getter_delegate()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyRequest
            {
                public string Name { get; set; } = string.Empty;
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        generated.ShouldContain("Setter = (dto, val) => ((t0)dto).Name = (string)val!");
    }

    [Fact]
    public void init_only_request_dto_property_emits_getter_without_setter()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyRequest
            {
                public string Name { get; init; } = string.Empty;
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        generated.ShouldNotContain("Setter =");
    }

    [Fact]
    public void endpoint_without_request_keyed_service_property_is_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("A")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"A\"");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void endpoint_with_propertyless_dto_keyed_service_is_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class EmptyDto { }

            public class MyEndpoint : Endpoint<EmptyDto, string>
            {
                [KeyedService("B")]
                public object MyService { get; set; } = default!;

                public override void Configure() { }

                public override Task HandleAsync(EmptyDto req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"B\"");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void keyed_init_property_emits_service_key_without_setter()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("KEY")]
                public object Service { get; init; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"KEY\"");
        generated.ShouldNotContain("Setter =");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void service_key_with_embedded_quote_is_escaped_in_generated_code()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\"a")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        // FormatLiteral emits the verbatim C# string literal, e.g. "tenant\"a"
        generated.ShouldContain(@"ServiceKey = ""tenant\""a""");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void service_key_with_backslash_is_escaped_in_generated_code()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\\path")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain(@"ServiceKey = ""tenant\\path""");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void generated_code_with_escaped_key_compiles_without_errors()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\"a")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
    }

    [Fact]
    public void unrelated_keyed_service_attribute_is_ignored()
    {
        const string source =
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace Other
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class KeyedServiceAttribute(string keyName) : Attribute;
            }

            namespace TestApp
            {
                public class MyEndpoint : EndpointWithoutRequest<string>
                {
                    [Other.KeyedService("NOT_FASTENDPOINTS")]
                    public object MyService { get; set; } = default!;

                    public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
                }
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldNotContain("NOT_FASTENDPOINTS");
        generated.ShouldContain("MyEndpoint");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static string RunGenerator(string source, out ImmutableArray<Diagnostic> generatorDiagnostics)
        => RunGenerator(source, out generatorDiagnostics, out _);

    static string RunGenerator(
        string source,
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        out Compilation outputCompilation)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            "TestApp",
            [syntaxTree],
            GetReferences(),
            new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ReflectionGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out generatorDiagnostics);

        var result = driver.GetRunResult();

        return result.GeneratedTrees
                     .FirstOrDefault(t => Path.GetFileName(t.FilePath) == "ReflectionData.g.cs")
                     ?.GetText().ToString()
               ?? string.Empty;
    }

    static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);

        foreach (var path in trustedPlatformAssemblies)
            yield return MetadataReference.CreateFromFile(path);

        yield return MetadataReference.CreateFromFile(typeof(Endpoint<>).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(HideFromDocsAttribute).Assembly.Location);
    }
}
