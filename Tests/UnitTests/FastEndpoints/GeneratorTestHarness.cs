using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generator;

internal sealed record GeneratorTestResult(
    string Generated,
    ImmutableArray<Diagnostic> Diagnostics,
    Compilation Output,
    Compilation Input,
    GeneratorDriver Driver,
    int GeneratedTreeCount);

internal static class GeneratorTestHarness
{
    internal static readonly CSharpParseOptions DefaultParseOptions = new(LanguageVersion.Preview);

    static readonly (string Path, MetadataReference Reference)[] _baseReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(static path => (path, (MetadataReference)MetadataReference.CreateFromFile(path)))
        .ToArray();

    static readonly HashSet<string> _basePaths = new(_baseReferences.Select(static r => r.Path), StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, MetadataReference> _extraReferenceCache = new(StringComparer.OrdinalIgnoreCase);

    internal static ImmutableArray<MetadataReference> GetReferences(params Assembly[] extraAssemblies)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>(_baseReferences.Length + extraAssemblies.Length);

        foreach (var (_, reference) in _baseReferences)
            builder.Add(reference);

        var seen = new HashSet<string>(_basePaths, StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in extraAssemblies)
        {
            var location = assembly.Location;

            if (seen.Add(location))
                builder.Add(_extraReferenceCache.GetOrAdd(location, static path => MetadataReference.CreateFromFile(path)));
        }

        return builder.ToImmutable();
    }

    internal static GeneratorTestResult Run<TGenerator>(
        IEnumerable<string> sources,
        IEnumerable<MetadataReference> references,
        string? assemblyName = "TestApp",
        CSharpParseOptions? parseOptions = null,
        string? hintFileName = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        parseOptions ??= DefaultParseOptions;

        var compilation = CSharpCompilation.Create(
            assemblyName,
            sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions)),
            references,
            new(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        return Step(driver, compilation, hintFileName);
    }

    internal static GeneratorTestResult Step(GeneratorDriver driver, Compilation compilation, string? hintFileName = null)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var trees = driver.GetRunResult().GeneratedTrees;
        var matchingTrees = hintFileName is null
                                ? trees
                                : trees.Where(t => Path.GetFileName(t.FilePath) == hintFileName).ToImmutableArray();

        var generated = matchingTrees.Length == 1
                            ? matchingTrees[0].GetText().ToString()
                            : string.Empty;

        return new(generated, diagnostics, outputCompilation, compilation, driver, matchingTrees.Length);
    }

    internal static void AssertCompiles(GeneratorTestResult result)
    {
        result.Diagnostics.ShouldBeEmpty();
        result.Output.GetDiagnostics()
              .Where(static d => d.Severity == DiagnosticSeverity.Error)
              .ShouldBeEmpty();
    }

    //generated code can't be annotated by users, so it must not raise CS0612 ([Obsolete]) or CS0618 ([Obsolete("...")]) for obsolete user types
    internal static void AssertNoObsoleteWarningsInGeneratedCode(GeneratorTestResult result)
        => result.Output.GetDiagnostics()
                 .Where(d => d.Id is "CS0612" or "CS0618" && !result.Input.SyntaxTrees.Contains(d.Location.SourceTree))
                 .ShouldBeEmpty();
}
