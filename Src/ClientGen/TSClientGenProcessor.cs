using NSwag.CodeGeneration;
using NSwag.CodeGeneration.TypeScript;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.ClientGen;

sealed class TsClientGenProcessor : IDocumentProcessor
{
    readonly string _destination;
    readonly ClientGeneratorOutputType _outputType;

    readonly TypeScriptClientGeneratorSettings _settings = new()
    {
        ClassName = "ApiClient",
        TypeScriptGeneratorSettings = { Namespace = "FastEndpoints" }
    };

    internal TsClientGenProcessor(Action<TypeScriptClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType)
    {
        settings(_settings);
        _destination = destination;
        _outputType = outputType;
    }

    public void Process(DocumentProcessorContext context)
        => _ = File.WriteAllTextAsync(_destination, new TypeScriptClientGenerator(context.Document, _settings).GenerateFile(_outputType));
}