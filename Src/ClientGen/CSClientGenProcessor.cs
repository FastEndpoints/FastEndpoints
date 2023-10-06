using NSwag.CodeGeneration;
using NSwag.CodeGeneration.CSharp;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.ClientGen;

sealed class CSClientGenProcessor : IDocumentProcessor
{
    readonly string destination;
    readonly ClientGeneratorOutputType outputType;

    readonly CSharpClientGeneratorSettings settings = new()
    {
        ClassName = "ApiClient",
        CSharpGeneratorSettings = { Namespace = "FastEndpoints" }
    };

    internal CSClientGenProcessor(Action<CSharpClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType)
    {
        settings(this.settings);
        this.destination = destination;
        this.outputType = outputType;
    }

    public void Process(DocumentProcessorContext context)
        => _ = File.WriteAllTextAsync(destination, new CSharpClientGenerator(context.Document, settings).GenerateFile(outputType));
}