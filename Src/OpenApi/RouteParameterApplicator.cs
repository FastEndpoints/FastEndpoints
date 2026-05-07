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
        => routeParameters.Select(static routeParameter => KeyValuePair.Create(routeParameter.Name, routeParameter))
                          .ToCaseInsensitiveDictionary(routeParameters.Count);

    internal void AddBoundRouteParameter(OpenApiOperation operation,
                                         PropertyInfo property,
                                         Dictionary<string, RouteParameterInfo> routeParameters,
                                         RequestTransformState state,
                                         string operationKey)
    {
        var bindName = GetPropertyMetadata(property).BindFrom?.Name ?? property.Name;

        if (!routeParameters.TryGetValue(bindName, out var matchingRouteParam))
            return;

        operation.RemovePropFromRequestBody(property, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);

        var appliedName = _parameterNameResolver.GetRouteName(matchingRouteParam.Name);

        if (TryNormalizeExistingPathParameter(operation, matchingRouteParam.Name, appliedName, property.PropertyType))
            return;

        if (!OperationParameterCollection.Has(operation, ParameterLocation.Path, appliedName))
            AddParameter(operation, appliedName, property, true);
        else
            OperationParameterCollection.UpdateSchema(operation, ParameterLocation.Path, appliedName, property.PropertyType, sharedCtx, docOpts.ShortSchemaNames);
    }

    internal void EnsureRouteParameters(OpenApiOperation operation, List<RouteParameterInfo> routeParameters)
    {
        for (var i = 0; i < routeParameters.Count; i++)
        {
            var routeParam = routeParameters[i];
            var appliedName = _parameterNameResolver.GetRouteName(routeParam.Name);
            var resolvedType = routeParam.ConstraintType;

            if (TryNormalizeExistingPathParameter(operation, routeParam.Name, appliedName, resolvedType))
                continue;

            AddParameter(operation, appliedName, null, true, resolvedType);
        }
    }

    bool TryNormalizeExistingPathParameter(OpenApiOperation operation, string routeParamName, string appliedName, Type? schemaType)
    {
        var existing = OperationParameterCollection.Find(operation, ParameterLocation.Path, appliedName) ??
                       OperationParameterCollection.Find(operation, ParameterLocation.Path, routeParamName);

        if (existing is null)
            return false;

        if (!string.Equals(existing.Name, appliedName, StringComparison.Ordinal))
            existing.Name = appliedName;

        if (schemaType is not null)
            existing.Schema = schemaType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames);

        return true;
    }

    void AddParameter(OpenApiOperation operation, string name, PropertyInfo? prop, bool? isRequired, Type? explicitType = null)
        => OperationParameterCollection.Add(
            operation,
            _parameterFactory.Create(name, ParameterLocation.Path, prop, isRequired, docOpts.ShortSchemaNames, explicitType));
}
