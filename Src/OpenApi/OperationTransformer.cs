using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiOperationTransformer
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();
    readonly ValidationSchemaTransformer _validationTransformer = new(docOpts, sharedCtx);
    readonly OperationMetadataTransformer _metadataTransformer = new(docOpts, sharedCtx);
    readonly RequestOperationTransformer _requestTransformer = new(docOpts, sharedCtx);
    readonly ResponseOperationTransformer _responseTransformer = new(docOpts, sharedCtx);

    sealed class TypeMetadata
    {
        public required PropertyInfo[] PublicInstanceProperties { get; init; }
        public required IReadOnlyDictionary<PropertyInfo, bool> NullableProperties { get; init; }
    }

    sealed class RouteParameterInfo
    {
        public required string Name { get; init; }
        public Type? ConstraintType { get; init; }
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var epDef = metadata.OfType<EndpointDefinition>().SingleOrDefault();

        docOpts.Services ??= context.ApplicationServices;
        _ = sharedCtx.ResolveNamingPolicy(context.ApplicationServices);
        sharedCtx.InitializeSharedRequestSchemaRefs(context.ApplicationServices, docOpts);

        // compute the document path for this operation
        var relativePath = context.Description.RelativePath?.TrimStart('~').TrimEnd('/') ?? "";
        var documentPath = "/" + RouteTemplateHelpers.StripConstraints(relativePath);
        var httpMethod = context.Description.HttpMethod?.ToUpperInvariant() ?? "GET";
        var operationKey = $"{httpMethod}:{documentPath}";

        if (epDef is null)
        {
            if (docOpts.ExcludeNonFastEndpoints)
                return Task.CompletedTask;

            // not a FastEndpoint
            sharedCtx.Operations[operationKey] = new()
            {
                OperationKey = operationKey,
                DocumentPath = documentPath,
                HttpMethod = httpMethod,
                Version = 0,
                StartingReleaseVersion = 0,
                DeprecatedAt = 0,
                IsFastEndpoint = false
            };

            return Task.CompletedTask;
        }

        // apply endpoint filter
        if (docOpts.EndpointFilter?.Invoke(epDef) == false)
            return Task.CompletedTask;

        // store version metadata for document transformer
        var bareRoute = BuildBareRoute(documentPath, GlobalConfig.EndpointRoutePrefix, epDef.Version.Current);
        var bareKey = $"{httpMethod}:{bareRoute}";

        sharedCtx.Operations[operationKey] = new()
        {
            OperationKey = bareKey,
            DocumentPath = documentPath,
            HttpMethod = httpMethod,
            Version = epDef.Version.Current,
            StartingReleaseVersion = epDef.Version.StartingReleaseVersion,
            DeprecatedAt = epDef.Version.DeprecatedAt,
            IsFastEndpoint = true
        };

        // operation ID
        var nameMetadata = metadata.OfType<EndpointNameMetadata>().LastOrDefault();
        if (nameMetadata is not null)
            operation.OperationId = nameMetadata.EndpointName;

        // auto-tagging
        _metadataTransformer.ApplyAutoTag(operation, epDef, bareRoute, metadata);

        // summary / description
        operation.Summary ??= epDef.EndpointSummary?.Summary;
        operation.Description ??= epDef.EndpointSummary?.Description;

        // deprecation from [Obsolete]
        if (epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null)
            operation.Deprecated = true;

        // handle request parameters
        var requestTransformState = _requestTransformer.HandleParameters(operation, context, epDef, documentPath);

        // handle [FromBody]/[FromForm] request body replacement + JSON Patch unwrap
        var promotedBodyPropertyName = _requestTransformer.ApplyBodyOverrides(operation, epDef);

        // apply endpoint-scoped validation to request body schemas after request body shape is finalized
        _validationTransformer.ApplyEndpointValidation(operation, context.ApplicationServices, epDef.ValidatorType, promotedBodyPropertyName?.Name);

        // apply parameter descriptions from EndpointSummary.Params and defaults from [DefaultValue]
        _requestTransformer.ApplyParameterMetadata(operation, epDef);

        // add missing responses from IProducesResponseTypeMetadata that ApiExplorer may have skipped
        // (e.g., 400 ErrorResponse with application/problem+json content type)
        _responseTransformer.AddMissingResponses(operation, metadata);

        // handle response descriptions
        _responseTransformer.ApplyDescriptions(operation, epDef, context);

        // fix binary response formats (MS OpenApi generates "byte" instead of "binary" for raw binary content types)
        _responseTransformer.FixBinaryFormats(operation);

        // handle response examples from EndpointSummary
        _responseTransformer.ApplyExamples(operation, epDef);

        // handle request body examples from EndpointSummary.RequestExamples
        _requestTransformer.ApplyExamples(operation, epDef, requestTransformState, promotedBodyPropertyName);

        // apply EndpointSummary.Params descriptions to request body schema properties
        _requestTransformer.ApplyParamDescriptionsToBodySchema(operation, epDef, requestTransformState, promotedBodyPropertyName);

        // handle response headers ([ToHeader] on response DTO + EndpointSummary.ResponseHeaders)
        _responseTransformer.AddHeaders(operation, epDef, metadata);

        // fix response polymorphism if enabled
        if (docOpts.UseOneOfForPolymorphism)
            _responseTransformer.FixPolymorphism(operation);

        // handle idempotency header
        _metadataTransformer.AddIdempotencyHeader(operation, epDef);

        // handle x402 headers
        _metadataTransformer.AddX402Headers(operation, epDef);

        // handle security requirements
        _metadataTransformer.ApplySecurityRequirements(operation, epDef, metadata, operationKey);

        // drop duplicate parameters introduced by Asp.Versioning (it adds the version route
        // segment as an extra path parameter alongside the one we derive from the endpoint).
        // ref: https://github.com/FastEndpoints/FastEndpoints/issues/560
        FinalizeParameters(operation);

        return Task.CompletedTask;
    }

    static PropertyInfo[] GetPublicInstanceProperties(Type type)
        => GetTypeMetadata(type).PublicInstanceProperties;

    static TypeMetadata GetTypeMetadata(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return _typeMetadataCache.GetOrAdd(type, CreateTypeMetadata);
    }

    static TypeMetadata CreateTypeMetadata(Type type)
    {
        var properties = type.GetProperties(PublicInstanceHierarchy);
        var nullabilityCtx = new NullabilityInfoContext();
        var nullableProperties = new Dictionary<PropertyInfo, bool>(properties.Length);

        foreach (var property in properties)
            nullableProperties[property] = nullabilityCtx.Create(property).WriteState is NullabilityState.Nullable;

        return new()
        {
            PublicInstanceProperties = properties,
            NullableProperties = new ReadOnlyDictionary<PropertyInfo, bool>(nullableProperties)
        };
    }

    static bool IsNullable(PropertyInfo prop)
    {
        var declaringType = prop.DeclaringType ?? prop.ReflectedType;

        return declaringType is not null &&
               GetTypeMetadata(declaringType).NullableProperties.TryGetValue(prop, out var isNullable) &&
               isNullable;
    }

    static Type? GetRequestDtoType(EndpointDefinition epDef)
        => epDef.ReqDtoType;

    static bool HasParameter(OpenApiOperation operation, ParameterLocation location, string name)
        => operation.Parameters?.Any(
               p => p.In == location &&
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) ==
           true;

    static void UpdateParameterSchema(OpenApiOperation operation, ParameterLocation location, string name, Type type, bool shortSchemaNames)
    {
        if (operation.Parameters is not { Count: > 0 })
            return;

        foreach (var param in operation.Parameters)
        {
            if (param.In != location || !string.Equals(param.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (param is OpenApiParameter concreteParam)
                concreteParam.Schema = type.GetSchemaForType(shortSchemaNames);

            return;
        }
    }

    static string BuildBareRoute(string documentPath, string? routePrefix, int endpointVersion)
    {
        var segments = documentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!string.IsNullOrWhiteSpace(routePrefix))
        {
            var prefixSegments = routePrefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var hasPrefix = segments.Count >= prefixSegments.Length;

            for (var i = 0; hasPrefix && i < prefixSegments.Length; i++)
                hasPrefix = string.Equals(segments[i], prefixSegments[i], StringComparison.Ordinal);

            if (hasPrefix)
                segments.RemoveRange(0, prefixSegments.Length);
        }

        if (endpointVersion > 0)
        {
            var versionSegment = $"{GlobalConfig.VersioningPrefix ?? "v"}{endpointVersion}";
            var versionIndex = segments.IndexOf(versionSegment);

            if (versionIndex >= 0)
                segments.RemoveAt(versionIndex);
        }

        return "/" + string.Join('/', segments);
    }

    static string? FindEndpointRouteTemplate(EndpointDefinition epDef, string documentPath)
    {
        if (epDef.Routes.Length == 0)
            return null;

        if (epDef.Routes.Length == 1)
            return epDef.Routes[0];

        foreach (var route in epDef.Routes)
        {
            var finalRoute = FastEndpoints.MainExtensions.BuildRoute(new StringBuilder(), epDef.Version.Current, route, epDef.OverriddenRoutePrefix);

            if (string.Equals(NormalizeRoutePath(finalRoute), documentPath, StringComparison.OrdinalIgnoreCase))
                return route;
        }

        return null;
    }

    static string NormalizeRoutePath(string route)
    {
        route = RouteTemplateHelpers.StripConstraints(route.TrimStart('~').TrimEnd('/'));

        return route.StartsWith('/') ? route : "/" + route;
    }

    static List<RouteParameterInfo> GetRouteParameters(string? relativePath)
    {
        var segments = RouteTemplateHelpers.GetParameterSegments(relativePath);
        var parameters = new List<RouteParameterInfo>(segments.Count);

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            parameters.Add(
                new()
                {
                    Name = RouteTemplateHelpers.NormalizeParameterName(segment),
                    ConstraintType = segment.TryResolveRouteConstraintType()
                });
        }

        return parameters;
    }

    static void FinalizeParameters(OpenApiOperation operation)
    {
        if (GlobalConfig.IsUsingAspVersioning)
            RemoveDuplicateParameters(operation);

        SortParameters(operation);
    }

    static void RemoveDuplicateParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var seen = new HashSet<(string Name, ParameterLocation Location)>();

        for (var i = operation.Parameters.Count - 1; i >= 0; i--)
        {
            var p = operation.Parameters[i];

            if (p.Name is null || p.In is not { } loc)
                continue;

            if (!seen.Add((p.Name, loc)))
                operation.Parameters.RemoveAt(i);
        }
    }

    static void SortParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var sorted = operation.Parameters
                              .OrderBy(
                                  p => p.In switch
                                  {
                                      ParameterLocation.Path => 0,
                                      ParameterLocation.Query => 1,
                                      ParameterLocation.Header => 2,
                                      ParameterLocation.Cookie => 3,
                                      _ => 4
                                  })
                              .ToList();

        operation.Parameters.Clear();

        foreach (var p in sorted)
            operation.Parameters.Add(p);
    }
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
