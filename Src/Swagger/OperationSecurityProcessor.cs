using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal sealed class OperationSecurityProcessor : IOperationProcessor
{
    private readonly string schemeName;

    public OperationSecurityProcessor(string schemeName)
    {
        this.schemeName = schemeName;
    }

    public bool Process(OperationProcessorContext context)
    {
        var epMeta = ((AspNetCoreOperationProcessorContext)context).ApiDescription.ActionDescriptor.EndpointMetadata;

        if (epMeta is null)
            return true;

        if (epMeta.OfType<AllowAnonymousAttribute>().Any() || !epMeta.OfType<AuthorizeAttribute>().Any())
            return true;

        var epDef = epMeta.OfType<EndpointDefinition>().SingleOrDefault();
        if (epDef == null)
        {
            if (epMeta.OfType<ControllerAttribute>().Any()) // it is an ApiController
                return true; // todo: return false if the documentation of such ApiControllers is not wanted.

            throw new InvalidOperationException(
                $"Endpoint `{context.ControllerType.FullName}` is missing an endpoint description. " +
                 "This may indicate an MvcController. Consider adding `[ApiExplorerSettings(IgnoreApi = true)]`");
        }

        var epSchemes = epDef.AuthSchemeNames;
        if (epSchemes?.Contains(schemeName) == false)
            return true;

        (context.OperationDescription.Operation.Security ??= new List<OpenApiSecurityRequirement>()).Add(
            new OpenApiSecurityRequirement {
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
