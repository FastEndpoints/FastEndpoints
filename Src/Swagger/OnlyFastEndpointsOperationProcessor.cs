using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;
public class OnlyFastEndpointsOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var metaData = ((AspNetCoreOperationProcessorContext)context).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epDef = metaData.OfType<EndpointDefinition>().SingleOrDefault();

        if (epDef is null)
            return false; //this is not a fastendpoint
        return true;
    }
}