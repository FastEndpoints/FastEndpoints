using FastEndpoints.Generator;
using Microsoft.CodeAnalysis.CSharp;

namespace Tests.SourceGeneratorTests;

public class SourceGenTests
{
    [Fact]
    public void TestMyGenerator()
    {
        var sourceCode = """
                         using System.Text.Json.Serialization;

                         namespace Endpoints.Forms;

                         [JsonConverter(typeof(FormDataConverter))]
                         public abstract class FormData
                         {
                             public int DataSourceId { get; set; } = 5; 
                             public string? Data1 { get; set; }
                         }
                         """;

        var generator = new ReflectionGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            nameof(SourceGenTests),
            [
                CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: TestContext.Current.CancellationToken)
            ],
            [
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        var runResult = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
    }
}