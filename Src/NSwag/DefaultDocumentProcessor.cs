using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.NSwag;

internal class DefaultDocumentProcessor : IDocumentProcessor
{
    private readonly int maxEndpointVersion;
    public DefaultDocumentProcessor(int maxEndpointVersion) => this.maxEndpointVersion = maxEndpointVersion;

    public void Process(DocumentProcessorContext context)
    {
        var yyy = context;
    }
}