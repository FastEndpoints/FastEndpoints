using NSwag.CodeGeneration;
using NSwag.CodeGeneration.TypeScript;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.ClientGen;

internal sealed class TSClientGenProcessor : IDocumentProcessor
{
    private readonly string destination;
    private readonly ClientGeneratorOutputType outputType;

    private readonly TypeScriptClientGeneratorSettings settings = new()
    {
        ClassName = "ApiClient",
        TypeScriptGeneratorSettings = { Namespace = "FastEndpoints" }
    };

    internal TSClientGenProcessor(Action<TypeScriptClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType)
    {
        settings(this.settings);
        this.destination = destination;
        this.outputType = outputType;
    }

    public void Process(DocumentProcessorContext context)
        => _ = File.WriteAllTextAsync(destination, new TypeScriptClientGenerator(context.Document, settings).GenerateFile(outputType));
}