using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.OperationReflectionCache;

namespace FastEndpoints.OpenApi;

sealed class RequestParameterMetadataApplicator(DocumentOptions docOpts, SharedContext sharedCtx)
{
    readonly OperationParameterNameResolver _parameterNameResolver = new(docOpts, sharedCtx);
    static readonly ParameterLookupKeyComparer _parameterLookupKeyComparer = new();

    JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

    internal void Apply(OpenApiOperation operation, EndpointDefinition epDef)
    {
        if (operation.Parameters is not { Count: > 0 })
            return;

        var requestDtoType = epDef.ReqDtoType;
        var requestProps = GetPublicInstanceProperties(requestDtoType);
        var requestPropLookup = BuildRequestPropertyLookup(requestProps);
        var paramDescriptions = epDef.EndpointSummary?.Params;
        var paramDescriptionLookup = paramDescriptions is { Count: > 0 }
                                         ? ParameterDescriptionLookup.Build(paramDescriptions)
                                         : null;

        foreach (var param in operation.Parameters)
        {
            if (param is not OpenApiParameter concreteParam)
                continue;

            var prop = concreteParam.Name is { } parameterName
                           ? FindRequestProperty(requestPropLookup, parameterName, concreteParam.In ?? ParameterLocation.Query)
                           : null;

            if (paramDescriptionLookup is not null)
            {
                var descriptionKey = prop?.Name ?? concreteParam.Name;

                if (descriptionKey is not null && paramDescriptionLookup.TryGetValue(descriptionKey, out var description))
                    concreteParam.Description = description;
            }

            if (string.IsNullOrWhiteSpace(concreteParam.Description) && prop is not null)
                concreteParam.Description = XmlDocLookup.GetPropertySummary(prop);

            if (prop is not null)
            {
                var defaultAttr = GetPropertyMetadata(prop).DefaultValue;

                if (defaultAttr?.Value is not null && concreteParam.Schema is OpenApiSchema paramSchema)
                    paramSchema.Default = defaultAttr.Value.JsonNodeFromObject(SerializerOptions);
            }
        }

        ApplyExampleRequestToParams(operation, epDef, requestPropLookup);
    }

    Dictionary<(ParameterLocation Location, string Name), PropertyInfo> BuildRequestPropertyLookup(PropertyInfo[] requestProps)
    {
        var lookup = new Dictionary<(ParameterLocation Location, string Name), PropertyInfo>(_parameterLookupKeyComparer);

        foreach (var prop in requestProps)
        {
            AddRequestPropertyLookup(lookup, prop, ParameterLocation.Path);
            AddRequestPropertyLookup(lookup, prop, ParameterLocation.Query);
            AddRequestPropertyLookup(lookup, prop, ParameterLocation.Header);
            AddRequestPropertyLookup(lookup, prop, ParameterLocation.Cookie);
        }

        return lookup;
    }

    void AddRequestPropertyLookup(Dictionary<(ParameterLocation Location, string Name), PropertyInfo> lookup, PropertyInfo property, ParameterLocation location)
        => lookup.TryAdd((location, _parameterNameResolver.GetEffectiveName(property, location)), property);

    static PropertyInfo? FindRequestProperty(Dictionary<(ParameterLocation Location, string Name), PropertyInfo>? lookup, string parameterName, ParameterLocation location)
    {
        if (lookup is null)
            return null;

        return lookup.GetValueOrDefault((location, parameterName));
    }

    void ApplyExampleRequestToParams(OpenApiOperation operation,
                                     EndpointDefinition epDef,
                                     Dictionary<(ParameterLocation Location, string Name), PropertyInfo>? requestPropLookup)
    {
        var exampleRequest = epDef.EndpointSummary?.ExampleRequest;

        if (exampleRequest is null || operation.Parameters is not { Count: > 0 })
            return;

        var exampleObj = exampleRequest.JsonObjectFromObject(SerializerOptions, exampleRequest.GetType());

        if (exampleObj is null)
            return;

        var examplePropertyLookup = BuildExamplePropertyLookup(exampleObj);

        foreach (var param in operation.Parameters)
        {
            if (param is not OpenApiParameter concreteParam)
                continue;

            if (concreteParam.Name is { } parameterName &&
                examplePropertyLookup.TryGetValue(parameterName, out var exampleValue) &&
                exampleValue is not null)
            {
                concreteParam.Example = exampleValue.DeepClone();

                continue;
            }

            var prop = concreteParam.Name is { } name
                           ? FindRequestProperty(requestPropLookup, name, concreteParam.In ?? ParameterLocation.Query)
                           : null;
            var propValue = prop?.DeclaringType?.IsInstanceOfType(exampleRequest) is true
                                ? prop.GetValue(exampleRequest)
                                : null;

            if (propValue is not null)
                concreteParam.Example = propValue.JsonNodeFromObject(SerializerOptions);
        }
    }

    static Dictionary<string, JsonNode?> BuildExamplePropertyLookup(JsonObject exampleObj)
    {
        var lookup = new Dictionary<string, JsonNode?>(exampleObj.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in exampleObj)
            lookup.TryAdd(key, value);

        return lookup;
    }

    sealed class ParameterLookupKeyComparer : IEqualityComparer<(ParameterLocation Location, string Name)>
    {
        public bool Equals((ParameterLocation Location, string Name) x, (ParameterLocation Location, string Name) y)
            => x.Location == y.Location && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((ParameterLocation Location, string Name) obj)
            => HashCode.Combine(obj.Location, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
