using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiOperationTransformer
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();
    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

    sealed class TypeMetadata
    {
        public required PropertyInfo[] PublicInstanceProperties { get; init; }
    }

    [GeneratedRegex(@"(?<=\{)[^}]+(?=\})")]
    private static partial Regex RouteParamsRegex();

    [GeneratedRegex("(?<={)\\**([^?:=}]+)[^}]*(?=})")]
    private static partial Regex RouteConstraintsRegex();

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
        var documentPath = "/" + StripRouteConstraints(relativePath);
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

        var metadataTransformer = new OperationMetadataTransformer(docOpts, sharedCtx);

        // auto-tagging
        metadataTransformer.ApplyAutoTag(operation, epDef, bareRoute, metadata);

        // summary / description
        operation.Summary ??= epDef.EndpointSummary?.Summary;
        operation.Description ??= epDef.EndpointSummary?.Description;

        // deprecation from [Obsolete]
        if (epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null)
            operation.Deprecated = true;

        var requestTransformer = new RequestOperationTransformer(docOpts, sharedCtx, NamingPolicy);
        var responseTransformer = new ResponseOperationTransformer(docOpts, sharedCtx, NamingPolicy);

        // handle request parameters
        var requestTransformState = requestTransformer.HandleParameters(operation, context, epDef, documentPath);

        // handle [FromBody]/[FromForm] request body replacement + JSON Patch unwrap
        requestTransformer.ApplyBodyOverrides(operation, epDef);

        // apply parameter descriptions from EndpointSummary.Params and defaults from [DefaultValue]
        requestTransformer.ApplyParameterMetadata(operation, epDef);

        // add missing responses from IProducesResponseTypeMetadata that ApiExplorer may have skipped
        // (e.g., 400 ErrorResponse with application/problem+json content type)
        responseTransformer.AddMissingResponses(operation, metadata);

        // handle response descriptions
        responseTransformer.ApplyDescriptions(operation, epDef, context);

        // fix binary response formats (MS OpenApi generates "byte" instead of "binary" for raw binary content types)
        responseTransformer.FixBinaryFormats(operation);

        // handle response examples from EndpointSummary
        responseTransformer.ApplyExamples(operation, epDef);

        // handle request body examples from EndpointSummary.RequestExamples
        requestTransformer.ApplyExamples(operation, epDef, requestTransformState);

        // apply EndpointSummary.Params descriptions to request body schema properties
        requestTransformer.ApplyParamDescriptionsToBodySchema(operation, epDef, requestTransformState);

        // handle response headers ([ToHeader] on response DTO + EndpointSummary.ResponseHeaders)
        responseTransformer.AddHeaders(operation, epDef, metadata);

        // fix response polymorphism if enabled
        if (docOpts.UseOneOfForPolymorphism)
            responseTransformer.FixPolymorphism(operation);

        // handle idempotency header
        metadataTransformer.AddIdempotencyHeader(operation, epDef);

        // handle x402 headers
        metadataTransformer.AddX402Headers(operation, epDef);

        // handle security requirements
        metadataTransformer.ApplySecurityRequirements(operation, epDef, metadata, operationKey);

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

        return _typeMetadataCache.GetOrAdd(type, static t => new() { PublicInstanceProperties = t.GetProperties(PublicInstanceHierarchy) });
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

    static string StripRouteConstraints(string relativePath)
    {
        if (!relativePath.Contains('{'))
            return relativePath;

        return RouteConstraintsRegex().Replace(relativePath, "$1");
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
        route = StripRouteConstraints(route.TrimStart('~').TrimEnd('/'));

        return route.StartsWith('/') ? route : "/" + route;
    }

    static List<RouteParameterInfo> GetRouteParameters(string? relativePath)
    {
        var matches = RouteParamsRegex().Matches(relativePath ?? string.Empty);
        var parameters = new List<RouteParameterInfo>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            var segment = matches[i].Value;
            parameters.Add(
                new()
                {
                    Name = GetRouteParameterName(segment),
                    ConstraintType = segment.TryResolveRouteConstraintType()
                });
        }

        return parameters;

        static string GetRouteParameterName(string segment)
        {
            var colonIdx = segment.IndexOf(':');
            var equalsIdx = segment.IndexOf('=');
            var splitIdx = colonIdx >= 0 && equalsIdx >= 0
                               ? Math.Min(colonIdx, equalsIdx)
                               : Math.Max(colonIdx, equalsIdx);
            var name = splitIdx >= 0 ? segment[..splitIdx] : segment;
            return name.TrimStart('*').TrimEnd('?');
        }
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

    internal static bool IsNullable(this PropertyInfo prop)
        => _nullabilityCtx.Create(prop).WriteState is NullabilityState.Nullable;

    static readonly NullabilityInfoContext _nullabilityCtx = new();
}
