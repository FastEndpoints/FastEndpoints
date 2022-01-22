using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.NSwag;

internal class DefaultDocumentProcessor : IDocumentProcessor
{
    private readonly int maxEpVer;
    public DefaultDocumentProcessor(int maxEndpointVersion) => maxEpVer = maxEndpointVersion;

    public void Process(DocumentProcessorContext ctx)
    {
        var yyy = ctx;

        // ctx.Document.Path
    }
}