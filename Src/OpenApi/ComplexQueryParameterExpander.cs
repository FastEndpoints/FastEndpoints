using System.Reflection;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.OperationTransformer;

namespace FastEndpoints.OpenApi;

sealed class ComplexQueryParameterExpander(OperationParameterFactory parameterFactory, OperationParameterNameResolver parameterNameResolver)
{
    internal bool TryAdd(OpenApiOperation operation, PropertyInfo property, bool shortSchemaNames)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (!GetPropertyMetadata(property).IsFromQuery ||
            !propertyType.IsComplexType() ||
            propertyType.IsCollection() ||
            OperationSchemaHelpers.TryGetDictionaryValueType(propertyType) is not null)
            return false;

        Add(operation, propertyType, prefix: null, shortSchemaNames, []);

        return true;
    }

    void Add(OpenApiOperation operation,
             Type type,
             string? prefix,
             bool shortSchemaNames,
             HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (!visited.Add(type))
            return;

        foreach (var prop in GetBindableRequestProperties(type))
        {
            if (GetPropertyMetadata(prop).IsHiddenFromDocs)
                continue;

            var propName = parameterNameResolver.GetQueryName(prop);
            var key = string.IsNullOrEmpty(prefix) ? propName : $"{prefix}.{propName}";
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (propType.IsComplexType() &&
                !propType.IsCollection() &&
                OperationSchemaHelpers.TryGetDictionaryValueType(propType) is null)
            {
                Add(operation, propType, key, shortSchemaNames, visited);

                continue;
            }

            if (propType.IsCollection() && OperationSchemaHelpers.TryGetCollectionElementType(propType) is { } elementType && elementType.IsComplexType())
            {
                Add(operation, elementType, $"{key}[0]", shortSchemaNames, visited);

                continue;
            }

            OperationParameterCollection.Add(
                operation,
                parameterFactory.Create(key, ParameterLocation.Query, prop, GetDontBindRequiredness(prop), shortSchemaNames));
        }

        visited.Remove(type);
    }

    static bool? GetDontBindRequiredness(PropertyInfo property)
        => property.GetCustomAttribute<DontBindAttribute>()?.IsRequired is true ? true : null;
}
