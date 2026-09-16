using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.OperationReflectionCache;
using static FastEndpoints.OpenApi.OperationTransformer;

namespace FastEndpoints.OpenApi;

sealed class RouteParameterApplicator(DocumentOptions docOpts, SharedContext sharedCtx)
{
    readonly OperationParameterFactory _parameterFactory = new(docOpts, sharedCtx);
    readonly OperationParameterNameResolver _parameterNameResolver = new(docOpts, sharedCtx);

    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

    internal static Dictionary<string, RouteParameterInfo> BuildLookup(List<RouteParameterInfo> routeParameters)
        => routeParameters.Select(static routeParameter => KeyValuePair.Create(routeParameter.Name, routeParameter)).ToCaseInsensitiveDictionary(routeParameters.Count);

    internal bool AddBoundRouteParameter(OpenApiOperation operation,
                                         PropertyInfo property,
                                         Dictionary<string, RouteParameterInfo> routeParameters,
                                         RequestTransformState state,
                                         string operationKey,
                                         OpenApiGenerationState generation)
    {
        var bindName = GetPropertyMetadata(property).BindFrom?.Name ?? property.Name;

        if (!routeParameters.TryGetValue(bindName, out var matchingRouteParam))
            return false;

        operation.RemovePropFromRequestBody(property, sharedCtx, generation, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);

        var appliedName = _parameterNameResolver.GetRouteName(matchingRouteParam.Name);

        if (TryNormalizeExistingPathParameter(operation, matchingRouteParam.Name, appliedName, property.PropertyType, generation) is { } existing)
        {
            state.RegisterBoundParameter(property, existing, NamingPolicy, docOpts.UsePropertyNamingPolicy);

            return true;
        }

        if (!OperationParameterCollection.Has(operation, ParameterLocation.Path, appliedName))
        {
            state.RegisterBoundParameter(property, AddParameter(operation, appliedName, property, true, generation), NamingPolicy, docOpts.UsePropertyNamingPolicy);

            return true;
        }

        OperationParameterCollection.UpdateSchema(operation, ParameterLocation.Path, appliedName, property.PropertyType, sharedCtx, generation, docOpts.ShortSchemaNames);

        if (OperationParameterCollection.Find(operation, ParameterLocation.Path, appliedName) is { } updated)
            state.RegisterBoundParameter(property, updated, NamingPolicy, docOpts.UsePropertyNamingPolicy);

        return true;
    }

    internal void EnsureRouteParameters(OpenApiOperation operation, List<RouteParameterInfo> routeParameters, OpenApiGenerationState generation)
    {
        for (var i = 0; i < routeParameters.Count; i++)
        {
            var routeParam = routeParameters[i];
            var appliedName = _parameterNameResolver.GetRouteName(routeParam.Name);
            var resolvedType = routeParam.ConstraintType;

            if (TryNormalizeExistingPathParameter(operation, routeParam.Name, appliedName, resolvedType, generation) is not null)
                continue;

            AddParameter(operation, appliedName, null, true, generation, resolvedType);
        }
    }

    OpenApiParameter? TryNormalizeExistingPathParameter(OpenApiOperation operation, string routeParamName, string appliedName, Type? schemaType, OpenApiGenerationState generation)
    {
        var existing = OperationParameterCollection.Find(operation, ParameterLocation.Path, appliedName) ??
                       OperationParameterCollection.Find(operation, ParameterLocation.Path, routeParamName);

        if (existing is null)
            return null;

        if (!string.Equals(existing.Name, appliedName, StringComparison.Ordinal))
            existing.Name = appliedName;

        if (schemaType is not null)
            existing.Schema = schemaType.GetSchemaForType(sharedCtx, generation, docOpts.ShortSchemaNames);

        return existing;
    }

    OpenApiParameter AddParameter(OpenApiOperation operation, string name, PropertyInfo? prop, bool? isRequired, OpenApiGenerationState generation, Type? explicitType = null)
    {
        var param = _parameterFactory.Create(name, ParameterLocation.Path, prop, generation, isRequired, docOpts.ShortSchemaNames, explicitType);
        OperationParameterCollection.Add(operation, param);

        return param;
    }
}
