using System.Collections.Concurrent;
using System.Reflection;
using static FastEndpoints.OpenApi.OperationTransformer;

namespace FastEndpoints.OpenApi;

static class PromotedBodyPropertyResolver
{
    static readonly ConcurrentDictionary<Type, PromotedBodyPropertySelection> _cache = new();

    internal static PromotedBodyPropertySelection Find(Type requestDtoType)
        => _cache.GetOrAdd(requestDtoType, CreateSelection);

    static PromotedBodyPropertySelection CreateSelection(Type requestDtoType)
    {
        PropertyInfo? fromBodyProp = null;
        PropertyInfo? fromFormProp = null;

        foreach (var prop in GetPublicInstanceProperties(requestDtoType))
        {
            var metadata = GetPropertyMetadata(prop);

            if (fromBodyProp is null && metadata.IsFromBody)
                fromBodyProp = prop;

            if (fromFormProp is null && metadata.IsFromForm)
                fromFormProp = prop;

            if (fromBodyProp is not null && fromFormProp is not null)
                break;
        }

        return new(fromBodyProp ?? fromFormProp, fromBodyProp, fromFormProp);
    }
}

readonly record struct PromotedBodyPropertySelection(PropertyInfo? Promoted, PropertyInfo? FromBody, PropertyInfo? FromForm);
