using NSwag.CodeGeneration;
using NSwag.CodeGeneration.CSharp;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.ClientGen;

sealed class CsClientGenProcessor : IDocumentProcessor
{
    readonly string _destination;
    readonly ClientGeneratorOutputType _outputType;

    readonly CSharpClientGeneratorSettings _settings = new()
    {
        ClassName = "ApiClient",
        CSharpGeneratorSettings = { Namespace = "FastEndpoints" }
    };

    internal CsClientGenProcessor(Action<CSharpClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType)
    {
        settings(_settings);
        _destination = destination;
        _outputType = outputType;
    }

    public void Process(DocumentProcessorContext context)
        => _ = File.WriteAllTextAsync(_destination, new CSharpClientGenerator(context.Document, _settings).GenerateFile(_outputType));
}