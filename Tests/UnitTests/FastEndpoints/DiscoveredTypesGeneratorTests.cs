using System.Collections.Immutable;
using FastEndpoints;
using FastEndpoints.Generator;
using FluentValidation;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Generator;

public class DiscoveredTypesGeneratorTests
{
    [Fact]
    public void validator_nested_in_open_generic_is_not_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class DocumentUpdateDto;

            public abstract class AbstractChecklistUpdateRequest;

            public class BaseUpdateRequestValidator<T> : Validator<T> where T : AbstractChecklistUpdateRequest
            {
                public sealed class DocumentUpdateDtoValidator : Validator<DocumentUpdateDto> { }

                sealed class PrivateDocumentUpdateDtoValidator : Validator<DocumentUpdateDto> { }
            }

            public class Outer<T>
            {
                public class Mid
                {
                    public sealed class DeepValidator : Validator<DocumentUpdateDto> { }
                }
            }

            public class Holder
            {
                public sealed class NestedInNonGeneric : Validator<DocumentUpdateDto> { }
            }

            public class TopLevelValidator : Validator<DocumentUpdateDto> { }

            public class ConcreteEndpoint : Endpoint<DocumentUpdateDto>
            {
                public override void Configure() => Get("/d");

                public override Task HandleAsync(DocumentUpdateDto req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.TopLevelValidator>()");
        generated.ShouldContain("Preserve<global::TestApp.ConcreteEndpoint>()");
        generated.ShouldContain("Preserve<global::TestApp.Holder.NestedInNonGeneric>()");
        generated.ShouldNotContain("DocumentUpdateDtoValidator");
        generated.ShouldNotContain("DeepValidator");
        generated.ShouldNotContain("BaseUpdateRequestValidator");
        generated.ShouldNotContain("<T>");
    }

    [Fact]
    public void type_named_like_root_namespace_does_not_shadow_emitted_names()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp.System
            {
                public class Marker;
            }

            namespace TestApp
            {
                public class TestApp;

                public class Type;

                public class Request;

                public class MyEndpoint : Endpoint<Request>
                {
                    public override Task HandleAsync(Request req, CancellationToken ct) => Task.CompletedTask;
                }
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.MyEndpoint>()");
    }

    [Fact]
    public void record_handler_is_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEvent;

            public record MyEventHandler : IEventHandler<MyEvent>
            {
                public Task HandleAsync(MyEvent eventModel, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.MyEventHandler>()");
    }

    [Fact]
    public void dont_register_attribute_omits_type_from_discovered_types()
    {
        const string source =
            """
            using FastEndpoints;

            namespace TestApp;

            [DontRegister]
            public class SkippedEndpoint : EndpointWithoutRequest
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }

            public class IncludedEndpoint : EndpointWithoutRequest
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.IncludedEndpoint>()");
        generated.ShouldNotContain("SkippedEndpoint");
    }


    [Fact]
    public void abstract_type_is_omitted_from_discovered_types()
    {
        const string source =
            """
            using FastEndpoints;

            namespace TestApp;

            public abstract class BaseOfEndpoints : EndpointWithoutRequest;

            public class ConcreteEndpoint : BaseOfEndpoints
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.ConcreteEndpoint>()");
        generated.ShouldNotContain("BaseOfEndpoints");
    }

    static readonly ImmutableArray<MetadataReference> _references =
        GeneratorTestHarness.GetReferences(typeof(Endpoint<>).Assembly, typeof(IValidator).Assembly);

    const string ImplicitUsingsSource =
        """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    static string RunGenerator(string source, out ImmutableArray<Diagnostic> generatorDiagnostics, out Compilation outputCompilation)
    {
        var result = GeneratorTestHarness.Run<DiscoveredTypesGenerator>(
            [ImplicitUsingsSource, source],
            _references,
            hintFileName: "DiscoveredTypes.g.cs");

        generatorDiagnostics = result.Diagnostics;
        outputCompilation = result.Output;

        return result.Generated;
    }
}
