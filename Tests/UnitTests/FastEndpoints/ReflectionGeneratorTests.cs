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

    [Fact]
    public void empty_source_emits_empty_reflection_registration()
    {
        var result = Run("");

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
    }

    [Fact]
    public void record_only_source_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
    }

    [Fact]
    public void generic_class_only_source_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public class Request<T>
            {
                public T Value { get; set; } = default!;
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
    }

    [Fact]
    public void class_without_endpoint_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public class Request
            {
                public string Name { get; set; } = "";
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
        result.Generated.ShouldNotContain("Getter =");
    }

    [Fact]
    public void response_alias_endpoint_emits_request_property_accessors()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            using CreateSessionResponse = Microsoft.AspNetCore.Http.HttpResults.Results<
                Microsoft.AspNetCore.Http.HttpResults.Created<MyApp.CreateSessionApiResponse>,
                Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>;

            namespace MyApp;

            public class CreateSessionApiResponse
            {
                public string Id { get; set; } = "";
            }

            public class CreateSessionRequest
            {
                public string Name { get; set; } = "";
            }

            public class CreateSessionEndpoint : Endpoint<CreateSessionRequest, CreateSessionResponse>
            {
                public override void Configure() { }

                public override Task HandleAsync(CreateSessionRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: false);
        result.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        result.Generated.ShouldContain("Setter = (dto, val) => ((t0)dto).Name = (string)val!");
        result.Generated.ShouldContain("CreateSessionRequest");
    }

    [Fact]
    public void assembly_name_is_sanitized_for_namespace_and_registration_method()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source, "My-App.Host");

        AssertRegistration(result, "My_App.Host", "MyAppHost", empty: true);
    }

    [Fact]
    public void null_assembly_name_falls_back_to_assembly()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source, null);

        AssertRegistration(result, "Assembly", "Assembly", empty: true);
    }

    [Fact]
    public void reused_driver_tracks_assembly_name_without_syntax_transform()
    {
        // Records are not class declarations, so Transform never runs. The namespace still has to follow the compilation.
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var first = Run(source, "First.App");
        AssertRegistration(first, "First.App", "FirstApp", empty: true);

        var second = Execute(first.Driver, first.Input.WithAssemblyName("Second-App"));
        AssertRegistration(second, "Second_App", "SecondApp", empty: true);
        second.Generated.ShouldNotContain("namespace First.App;");
        second.Generated.ShouldNotContain("AddFromFirstApp");
    }

    [Fact]
    public void reused_driver_keeps_request_accessors_when_assembly_name_changes()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                public string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var first = Run(source, "First.App");
        AssertRegistration(first, "First.App", "FirstApp", empty: false);
        first.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");

        var second = Execute(first.Driver, first.Input.WithAssemblyName("Second.App"));
        AssertRegistration(second, "Second.App", "SecondApp", empty: false);
        second.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        second.Generated.ShouldNotContain("namespace First.App;");
        second.Generated.ShouldNotContain("AddFromFirstApp");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static string RunGenerator(string source, out ImmutableArray<Diagnostic> generatorDiagnostics)
        => RunGenerator(source, out generatorDiagnostics, out _);

    static string RunGenerator(
        string source,
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        out Compilation outputCompilation)
    {
        var result = Run(source);
        generatorDiagnostics = result.Diagnostics;
        outputCompilation = result.Output;

        return result.Generated;
    }

    static GeneratorResult Run(string source, string? assemblyName = "TestApp")
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            GetReferences(),
            new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ReflectionGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        return Execute(driver, compilation);
    }

    static GeneratorResult Execute(GeneratorDriver driver, Compilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var reflectionTrees = driver.GetRunResult()
                                    .GeneratedTrees
                                    .Where(static t => Path.GetFileName(t.FilePath) == "ReflectionData.g.cs")
                                    .ToArray();

        var generated = reflectionTrees.Length == 1
                            ? reflectionTrees[0].GetText().ToString()
                            : string.Empty;

        return new(generated, diagnostics, outputCompilation, compilation, driver, reflectionTrees.Length);
    }

    static void AssertRegistration(GeneratorResult result, string expectedNamespace, string expectedMethod, bool empty)
    {
        result.Input.GetDiagnostics()
              .Where(static d => d.Severity == DiagnosticSeverity.Error)
              .ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
        result.ReflectionTreeCount.ShouldBe(1);
        result.Output.GetDiagnostics()
              .Where(static d => d.Severity == DiagnosticSeverity.Error)
              .ShouldBeEmpty();
        result.Generated.ShouldContain($"namespace {expectedNamespace};");
        result.Generated.ShouldContain($"public static ReflectionCache AddFrom{expectedMethod}(this ReflectionCache cache)");
        result.Generated.ShouldContain("return cache;");

        if (empty)
            result.Generated.ShouldNotContain("cache.TryAdd");
    }

    static IEnumerable<MetadataReference> GetReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);

        foreach (var path in trustedPlatformAssemblies)
            paths.Add(path);

        paths.Add(typeof(Endpoint<>).Assembly.Location);
        paths.Add(typeof(HideFromDocsAttribute).Assembly.Location);
        paths.Add(typeof(Microsoft.AspNetCore.Http.IResult).Assembly.Location);
        paths.Add(typeof(Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult).Assembly.Location);

        return paths.Select(static path => MetadataReference.CreateFromFile(path));
    }

    sealed record GeneratorResult(
        string Generated,
        ImmutableArray<Diagnostic> Diagnostics,
        Compilation Output,
        Compilation Input,
        GeneratorDriver Driver,
        int ReflectionTreeCount);
}
