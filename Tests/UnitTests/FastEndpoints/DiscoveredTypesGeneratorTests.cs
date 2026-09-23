using System.Collections.Immutable;
using FastEndpoints;
using FastEndpoints.Generator;
using FluentValidation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        generated.ShouldContain("Preserve<TestApp.TopLevelValidator>()");
        generated.ShouldContain("Preserve<TestApp.ConcreteEndpoint>()");
        generated.ShouldContain("Preserve<TestApp.Holder.NestedInNonGeneric>()");
        generated.ShouldNotContain("DocumentUpdateDtoValidator");
        generated.ShouldNotContain("DeepValidator");
        generated.ShouldNotContain("BaseUpdateRequestValidator");
        generated.ShouldNotContain("<T>");
    }

    static string RunGenerator(string source, out ImmutableArray<Diagnostic> generatorDiagnostics, out Compilation outputCompilation)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var implicitUsings = CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Threading;
            global using System.Threading.Tasks;
            """,
            parseOptions);

        var compilation = CSharpCompilation.Create(
            "TestApp",
            [implicitUsings, syntaxTree],
            GetReferences(),
            new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DiscoveredTypesGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out generatorDiagnostics);

        return driver.GetRunResult()
                     .GeneratedTrees
                     .FirstOrDefault(t => Path.GetFileName(t.FilePath) == "DiscoveredTypes.g.cs")
                     ?.GetText()
                     .ToString()
               ?? string.Empty;
    }

    static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);

        foreach (var path in trustedPlatformAssemblies)
            yield return MetadataReference.CreateFromFile(path);

        yield return MetadataReference.CreateFromFile(typeof(Endpoint<>).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(IValidator).Assembly.Location);
    }
}
