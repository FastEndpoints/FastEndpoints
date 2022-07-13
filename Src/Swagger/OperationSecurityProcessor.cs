using Microsoft.AspNetCore.Authorization;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal class OperationSecurityProcessor : IOperationProcessor
{
    private readonly string schemeName;

    public OperationSecurityProcessor(string schemeName)
        => this.schemeName = schemeName;

    public bool Process(OperationProcessorContext context)
    {
        var epMeta = ((AspNetCoreOperationProcessorContext)context).ApiDescription.ActionDescriptor.EndpointMetadata;

        if (epMeta is null)
            return true;

        if (epMeta.OfType<AllowAnonymousAttribute>().Any() || !epMeta.OfType<AuthorizeAttribute>().Any())
            return true;

        var schemesList = epMeta.OfType<EndpointDefinition>().SingleOrDefault();
        if (schemesList == null)
        {
            throw new InvalidOperationsException($"Endpoint {context} missing an EndpointDefinition attribute for scheme {schemeName}");
        }

        var epSchemes = schemesList.AuthSchemes;
        if (epSchemes?.Contains(schemeName) == false)
            return true;

        (context.OperationDescription.Operation.Security ??= new List<OpenApiSecurityRequirement>()).Add(
            new OpenApiSecurityRequirement
            {
                { schemeName, BuildScopes(epMeta!.OfType<AuthorizeAttribute>()) }
            });

        return true;
    }

    private static IEnumerable<string> BuildScopes(IEnumerable<AuthorizeAttribute> authorizeAttributes)
    {
        return authorizeAttributes
            .Where(a => a.Roles != null)
            .SelectMany(a => a.Roles!.Split(','))
            .Distinct();
    }
}
