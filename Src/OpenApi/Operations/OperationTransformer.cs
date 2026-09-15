using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiOperationTransformer
{
    readonly ValidationSchemaTransformer _validationTransformer = new(docOpts, sharedCtx);
    readonly OperationMetadataTransformer _metadataTransformer = new(docOpts, sharedCtx);
    readonly RequestOperationTransformer _requestTransformer = new(docOpts, sharedCtx);
    readonly ResponseOperationTransformer _responseTransformer = new(docOpts, sharedCtx);

    internal sealed class RouteParameterInfo
    {
        public required string Name { get; init; }
        public Type? ConstraintType { get; init; }
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var epDef = metadata.OfType<EndpointDefinition>().SingleOrDefault();

        docOpts.Services ??= context.ApplicationServices;
        sharedCtx.ResolveNamingPolicy();
        var generation = sharedCtx.ForRequired(context.Document);
        // compute the document path for this operation
        var relativePath = context.Description.RelativePath ?? "";
        var documentPath = RouteTemplateHelpers.NormalizePath(relativePath);
        var httpMethod = context.Description.HttpMethod?.ToUpperInvariant() ?? "GET";
        var operationKey = CreateOperationKey(httpMethod, documentPath);

        if (epDef is null)
        {
            if (docOpts.ExcludeNonFastEndpoints)
                return Task.CompletedTask;

            RegisterNonFastEndpointOperation(generation, operationKey, documentPath, httpMethod);
            _metadataTransformer.ApplySecurityRequirements(operation, null, metadata, operationKey, generation);

            return Task.CompletedTask;
        }

        // apply endpoint filter
        if (docOpts.EndpointFilter?.Invoke(epDef) == false)
            return Task.CompletedTask;

        // store version metadata for document transformer
        var bareRoute = BuildBareRoute(documentPath, GlobalConfig.EndpointRoutePrefix, epDef.Version.Current);
        RegisterFastEndpointOperation(generation, operationKey, httpMethod, documentPath, bareRoute, epDef);

        // operation ID
        var nameMetadata = metadata.OfType<EndpointNameMetadata>().LastOrDefault();
        if (nameMetadata is not null)
            operation.OperationId = nameMetadata.EndpointName;

        // auto-tagging
        _metadataTransformer.ApplyAutoTag(operation, epDef, bareRoute, metadata);

        // summary / description
        operation.Summary ??= epDef.EndpointSummary?.Summary;
        operation.Description ??= epDef.EndpointSummary?.Description;

        if (string.IsNullOrWhiteSpace(epDef.EndpointSummary?.Summary) && string.IsNullOrWhiteSpace(operation.Summary))
            operation.Summary = XmlDocLookup.GetTypeSummary(epDef.EndpointType);

        if (string.IsNullOrWhiteSpace(epDef.EndpointSummary?.Description) && string.IsNullOrWhiteSpace(operation.Description))
            operation.Description = XmlDocLookup.GetTypeRemarks(epDef.EndpointType);

        // deprecation from [Obsolete]
        if (epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null)
            operation.Deprecated = true;

        // handle request parameters
        var requestTransformState = _requestTransformer.HandleParameters(operation, context, epDef, documentPath, operationKey, generation);

        // handle [FromBody]/[FromForm] request body replacement + JSON Patch unwrap
        var promotedBodyPropertyName = _requestTransformer.ApplyBodyOverrides(operation, epDef, operationKey, generation);

        // apply endpoint-scoped validation to request body and DTO-bound parameter schemas after request shape is finalized
        _validationTransformer.ApplyEndpointValidation(
            operation,
            context.ApplicationServices,
            epDef.ValidatorType,
            operationKey,
            requestTransformState,
            generation,
            promotedBodyPropertyName?.Name);

        // apply parameter descriptions from EndpointSummary.Params and defaults from [DefaultValue]
        _requestTransformer.ApplyParameterMetadata(operation, epDef);

        // add missing responses from IProducesResponseTypeMetadata that ApiExplorer may have skipped
        // (e.g., 400 ErrorResponse with application/problem+json content type)
        _responseTransformer.AddMissingResponses(operation, metadata, generation);

        // handle response descriptions
        _responseTransformer.ApplyDescriptions(operation, epDef, context, operationKey, generation);

        // fix binary response formats (MS OpenApi generates "byte" instead of "binary" for raw binary content types)
        _responseTransformer.FixBinaryFormats(operation, operationKey, generation);

        // handle response examples from EndpointSummary
        _responseTransformer.ApplyExamples(operation, epDef, metadata);

        // handle request body examples from EndpointSummary.RequestExamples
        _requestTransformer.ApplyExamples(operation, epDef, requestTransformState, promotedBodyPropertyName, generation);

        // apply EndpointSummary.Params descriptions to request body schema properties
        _requestTransformer.ApplyParamDescriptionsToBodySchema(operation, epDef, requestTransformState, promotedBodyPropertyName, operationKey, generation);

        // handle response headers ([ToHeader] on response DTO + EndpointSummary.ResponseHeaders)
        _responseTransformer.AddHeaders(operation, epDef, metadata, generation);

        // fix response polymorphism if enabled
        if (docOpts.UseOneOfForPolymorphism)
            _responseTransformer.FixPolymorphism(operation, operationKey, generation);

        // handle idempotency header
        _metadataTransformer.AddIdempotencyHeader(operation, epDef, generation);

        // handle x402 headers
        _metadataTransformer.AddX402Headers(operation, epDef);

        // handle security requirements
        _metadataTransformer.ApplySecurityRequirements(operation, epDef, metadata, operationKey, generation);

        // drop duplicate parameters introduced by Asp.Versioning (it adds the version route
        // segment as an extra path parameter alongside the one we derive from the endpoint).
        // ref: https://github.com/FastEndpoints/FastEndpoints/issues/560
        OperationParameterFinalizer.Finalize(operation);

        return Task.CompletedTask;
    }

    static string CreateOperationKey(string httpMethod, string documentPath)
        => $"{httpMethod}:{documentPath}";

    void RegisterNonFastEndpointOperation(OpenApiGenerationState generation, string operationKey, string documentPath, string httpMethod)
        => generation.Operations[operationKey] = new()
        {
            OperationKey = operationKey,
            DocumentPath = documentPath,
            HttpMethod = httpMethod,
            Version = 0,
            StartingReleaseVersion = 0,
            DeprecatedAt = 0,
            IsFastEndpoint = false
        };

    void RegisterFastEndpointOperation(OpenApiGenerationState generation, string operationKey, string httpMethod, string documentPath, string bareRoute, EndpointDefinition epDef)
        => generation.Operations[operationKey] = new()
        {
            OperationKey = CreateOperationKey(httpMethod, bareRoute),
            DocumentPath = documentPath,
            HttpMethod = httpMethod,
            Version = epDef.Version.Current,
            StartingReleaseVersion = epDef.Version.StartingReleaseVersion,
            DeprecatedAt = epDef.Version.DeprecatedAt,
            IsFastEndpoint = true
        };

}

static class OperationTransformerExtensions
{
    internal static string ApplyPropNamingPolicy(this string paramName, DocumentOptions documentOptions, JsonNamingPolicy? namingPolicy)
    {
        if (!documentOptions.UsePropertyNamingPolicy)
            return paramName;

        return namingPolicy?.ConvertName(paramName) ?? paramName;
    }

    internal static string GetOpenApiRouteParameterName(this string routeParamName, DocumentOptions documentOptions, JsonNamingPolicy? namingPolicy)
        => routeParamName.ApplyPropNamingPolicy(documentOptions, namingPolicy);
}
