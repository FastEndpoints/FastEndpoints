using Microsoft.AspNetCore.Authorization;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using NSwag.Generation.Processors.Security;

namespace FastEndpoints.Swagger;

sealed class OperationSecurityProcessor(string schemeName) : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var epMeta = ((AspNetCoreOperationProcessorContext)context).ApiDescription.ActionDescriptor.EndpointMetadata;
        var authNotRequired = epMeta.OfType<AllowAnonymousAttribute>().Any() || !epMeta.OfType<AuthorizeAttribute>().Any();
        var epDef = epMeta.OfType<EndpointDefinition>().SingleOrDefault();

        if (authNotRequired)
            return true;

        if (epDef is null) //this is not a fastendpoint
            return new OperationSecurityScopeProcessor(schemeName).Process(context);

        var epSchemes = epDef.AuthSchemeNames;

        if (epSchemes?.Contains(schemeName) == false)
            return true;

        (context.OperationDescription.Operation.Security ??= new List<OpenApiSecurityRequirement>()).Add(
            new()
            {
                { schemeName, BuildScopes(epMeta.OfType<AuthorizeAttribute>()) }
            });

        return true;
    }

    static IEnumerable<string> BuildScopes(IEnumerable<AuthorizeAttribute> authorizeAttributes)
    {
        return authorizeAttributes
               .Where(a => a.Roles != null)
               .SelectMany(a => a.Roles!.Split(','))
               .Distinct();
    }
}