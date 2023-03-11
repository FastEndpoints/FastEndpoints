using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal sealed class EndpointFilter : IOperationProcessor
{
    private readonly Func<EndpointDefinition, bool> _filter;

    public EndpointFilter(Func<EndpointDefinition, bool> filter)
    {
        _filter = filter;
    }

    public bool Process(OperationProcessorContext ctx)
    {
        var def = ((AspNetCoreOperationProcessorContext)ctx)
            .ApiDescription
            .ActionDescriptor
            .EndpointMetadata
            .OfType<EndpointDefinition>()
            .SingleOrDefault();

        if (def is null)
        {
            return true; //this is not a fast endpoint
        }

        return _filter(def);
    }
}
