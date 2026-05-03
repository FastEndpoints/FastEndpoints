using System.Reflection;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    static readonly HashSet<string> _setLikeGenericTypes =
    [
        "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Frozen.FrozenSet`1",
        "System.Collections.Immutable.IImmutableSet`1",
        "System.Collections.Immutable.ImmutableHashSet`1",
        "System.Collections.Immutable.ImmutableSortedSet`1"
    ];

    internal static void ApplyUniqueItems(OpenApiSchema schema, Type collectionType, MemberInfo? member = null)
    {
        if (!IsUniqueItemsCollection(collectionType, member))
            return;

        schema.UniqueItems = true;
    }

    static bool IsUniqueItemsCollection(Type type, MemberInfo? member)
    {
        type = type.GetUnderlyingType();

        if (type == typeof(string) || type == typeof(byte[]))
            return false;

        var elementType = TryGetCollectionElementType(type);

        if (elementType is null)
            return false;

        if (member?.IsDefined(typeof(UniqueItemsAttribute), true) is true)
            return true;

        return IsSetLikeCollectionType(type) && !elementType.GetUnderlyingType().IsComplexType();
    }

    static bool IsSetLikeCollectionType(Type type)
    {
        if (MatchesSetLikeType(type))
            return true;

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (MatchesSetLikeType(interfaceType))
                return true;
        }

        return false;

        static bool MatchesSetLikeType(Type candidate)
        {
            if (!candidate.IsGenericType)
                return false;

            var genericDef = candidate.IsGenericTypeDefinition ? candidate : candidate.GetGenericTypeDefinition();
            var genericName = genericDef.FullName;

            return genericName is not null && _setLikeGenericTypes.Contains(genericName);
        }
    }
}
